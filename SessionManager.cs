#nullable enable


using Microsoft.Extensions.Logging;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Proto;
using Color = Proto.Color;
using Serilog;


public delegate void DlgtGameStateChange(GameStateChange e);

struct PlayerCardState
{
    
}

struct PlayerHand
{
    private List<uint> _inHand;
    private List<uint> _available;

   public PlayerHand(List<uint> inHand)
    {
        _inHand = inHand;
        _available = new List<uint>();
    }
}

struct ArenaCards
{
    private List<Pile> piles;

    void RegisterArenaActions(List<ArenaStateChange> changes)
    {
        foreach (var c in changes)
        {
            if (c is { Card: 0, Action: StateChangeAction.Add }&&c.PileIndex==piles.Count)
            {
                piles.Add(new Pile(c.Card));
                SessionManager.Log(LogLevel.Information,"Added new card to new pile");
            }
            else if (c.Card!=0 && c.Action==StateChangeAction.Add && c.PileIndex<piles.Count)
            {
                piles[(int)c.PileIndex].AddCard(c.Card); 
            }
            else
            {
                SessionManager.Log(LogLevel.Warning, "Invalid state for arena state change");
            }
        }
    }
}

struct Pile
{
    private List<uint> cards;

    public Pile(uint card)
    {
        cards = new List<uint> { card };
    }

    public void AddCard(uint card)
    {
        cards.Add(card);
    }
}

struct GameState
{
    private ArenaCards _arenaCards;
    private PlayerHand _playerHand;

    GameState(List<uint> playerHand)
    {
        _arenaCards = new ArenaCards();
        _playerHand = new PlayerHand(playerHand);
    }

}
public partial class SessionManager : Node
{
    public GrpcChannel GrpcChannel;
    [Export] public ItemList? SessionList;
    [Export] public Container? SessionView;
    public SessionService.SessionServiceClient? SessionServiceClient;
    private static Microsoft.Extensions.Logging.ILogger _logger;
    
    public delegate void ArenaActionDelegate((uint,Color)[] decks);

    private ArenaActionDelegate? _arenaActionDelegate;
    private ArenaCards _arenaCards = new ArenaCards();
    public delegate void PlayerActionDelegate(Proto.PlayerPlay play);

    private PlayerActionDelegate? _playerActionDelegateHandler;


    public GameService.GameServiceClient? GameServiceClient;

   
    public Card[]? CardsContext;
    public Player? PlayerContext;
    public Session? SessionContext;

    private Task? serverStreamTask;

    public DlgtGameStateChange? DlgtGameStateChange;
    private bool isGameStarted = false;
    private uint _eventCounter = 0;


    private AsyncClientStreamingCall<ClientEvent, Empty>? clientStream;
    List<uint> serverEventsInFlight = new();
    Channel<uint> serverEventsInFlightChannel = Channel.CreateBounded<uint>(new BoundedChannelOptions(capacity:10)
    {
        SingleWriter = true,
        SingleReader = false,
        AllowSynchronousContinuations = true
        
    });

    public void RegisterArenaAction(ArenaActionDelegate action)
    {
        if (_arenaActionDelegate is null)
        {
            _arenaActionDelegate = new ArenaActionDelegate(action);
        }
        else
        {
            _arenaActionDelegate += action;
        }
    }

    public void RegisterPlayerPlayAction(PlayerActionDelegate action)
    {
        if (_playerActionDelegateHandler is null)
        {
            _playerActionDelegateHandler = new PlayerActionDelegate(action);
        }
        else
        {
            _playerActionDelegateHandler += action;
        }
    }

    public static void Log(LogLevel logLevel, string message, [CallerMemberName] string memberName = null,
        [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
    {
        if (_logger is null)
        {
            GD.PrintErr("Logger is null");
            return;
        }
        _logger.Log(logLevel, $"In function {memberName} ({filePath}:{line}): {message}");
    }

    public async Task RequestAcknowledgementEvent(uint eventId)
    {
        //add event to in flight list
        lock (serverEventsInFlight)
        {
            serverEventsInFlight.Add(eventId);
        }
      //read from the channel until it yields an event with this id
      await foreach(var e in serverEventsInFlightChannel.Reader.ReadAllAsync())
      {
          Log(LogLevel.Information,"Got event from channel " + e);
          if (e != eventId) continue;
          Log(LogLevel.Information,$"Event {e} acked");
          return;
      }
      
    }
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Microsoft.Extensions.Logging.ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options => { options.IncludeScopes = true; });
        });
        _logger = loggerFactory.CreateLogger<SessionManager>();
        Log(LogLevel.Warning, "Session manager is ready"); 
        GrpcChannel = GrpcChannel.ForAddress("http://localhost:50051");
        SessionServiceClient = new SessionService.SessionServiceClient(GrpcChannel);
        
    }

    private async Task SendCountedClientEvent(AsyncClientStreamingCall<ClientEvent, Empty> stream,
        ClientEvent clientEvent)
    {

        var eid = Interlocked.Increment(ref _eventCounter) - 1;
        clientEvent.EventId = eid;
        //send event to server
        GD.Print("Send event to server" + clientEvent);
        stream.RequestStream.WriteAsync(clientEvent).Wait();
        //wait for acknowledgement
        await RequestAcknowledgementEvent(clientEvent.EventId);
        Log(LogLevel.Information,"Event " + clientEvent.EventId + " acknowledged");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }


    public void StartGame(Player player)
    {
        this.PlayerContext = player;
    }

    public async Task OpenEventStream(Player player)
    {
        GD.Print("Opening event stream");
        SessionContext = SessionServiceClient?.GetSession(new GetSessionRq
        {
            SessionId = player.SessionId
        });
        PlayerContext = player;
        GenerateAllCards((uint)SessionContext.Players.Count);
        GameServiceClient = new GameService.GameServiceClient(GrpcChannel);
        var serverStream = GameServiceClient.OpenEventStream(player);
        serverStreamTask = ProcessServerEvents(serverStream);
        clientStream = GameServiceClient.OpenClientEventStream();
        //first send open stream event
        var init_event = new ClientEvent();
        init_event.EventId = 0;
        init_event.OpenStream = new ClientInitOpenStream
        {
            Player = player
        };
        await SendCountedClientEvent(clientStream, init_event);
    }

    private Task ProcessServerEvents(AsyncServerStreamingCall<ServerEvent> eventStream)
    {
        return Task.Run(async () =>
        {
            GD.Print("Started server event stream processing");
            try
            {
                await foreach (var serverEvent in eventStream.ResponseStream.ReadAllAsync())
                {
                    switch (serverEvent.EventCase)
                    {
                        case ServerEvent.EventOneofCase.None:
                            break;
                        case ServerEvent.EventOneofCase.GameStateChange:
                            var e = serverEvent.GameStateChange!;
                            if (e.ArenaStateChanges.Count > 0)
                            {
                                var card = e.ArenaStateChanges.Select((v) => { return CardsContext[v.Card]; });
                            }
                            break;
                        case ServerEvent.EventOneofCase.Acknowledge:
                            var ack = serverEvent.Acknowledge!;
                            //see if there's any client event that server acknowledged
                            GD.Print("Got acknowledge event with id " + ack.EventId);
                            await serverEventsInFlightChannel.Writer.WriteAsync(ack.EventId);
                            Log(LogLevel.Information,"Wrote event " + ack.EventId + " to channel");
                            break;
                        case ServerEvent.EventOneofCase.ServerGameStateAction:
                            break;
                        case ServerEvent.EventOneofCase.RequestStartGame:
                            var startGameEvent = serverEvent.RequestStartGame!;
                            Log(LogLevel.Information, "Got start game event");
                            if (SessionContext != null) GenerateAllCards((uint)SessionContext.Players.Count);
                            isGameStarted = true;
                            //sending acknowledge event
                            var ackEvent = new ClientEvent
                            {
                                EventId =serverEvent.EventId,
                                Acknowledge = new Acknowledge
                                {
                                    AcknowledgementType = EAcknowledgementType.Accepted,
                                    EventId = serverEvent.EventId
                                }
                                
                            };
                            Log(LogLevel.Information, "Sending client acknowledge event");
                            if (clientStream != null) await clientStream.RequestStream.WriteAsync(ackEvent);
                            break;
                        case ServerEvent.EventOneofCase.ChangeDrawRate:
                            break;
                        case ServerEvent.EventOneofCase.ConfirmGameStart:
                            //only gets sent to player that initiated game start (admin)
                            Log(LogLevel.Information, "Got confirm game start event");
                            isGameStarted = true;
                            var ackEvent2 = new ClientEvent
                            {
                                EventId =serverEvent.EventId,
                                Acknowledge = new Acknowledge
                                {
                                    AcknowledgementType = EAcknowledgementType.Accepted,
                                    EventId = serverEvent.EventId
                                }
                                
                            };
                            Log(LogLevel.Information, "Sending client acknowledge event");
                            if (clientStream != null) await clientStream.RequestStream.WriteAsync(ackEvent2);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                GD.Print("Finished server event stream processing");
            }
            catch (Grpc.Core.RpcException e)
            {
                Console.WriteLine(e);
            }
        });
    }

    public void _OnGameStateChange(GameStateChange e)
    {
        GD.Print("Arena action " + e);
    }

    public async Task RequestStartGame()
    {
        if (clientStream is not null)
        {
            if (PlayerContext != null && PlayerContext.IsSessionAdmin && !isGameStarted)
            {
                var clientEvent = new ClientEvent
                {
                    EventId = _eventCounter,
                    StartGame = new StartGameEvent
                    {
                        Player = PlayerContext,
                        Prefs = new GamePrefs
                        {
                            BlitzDeduction = 10,
                            DrawRate = 3,
                            PostPileSize = 3,
                            ScoreToWin = 72,
                        }
                    }
                };
                isGameStarted = true;
                //send start game event
                Log(LogLevel.Information,"Trying to send start game event");
                SendCountedClientEvent(clientStream, clientEvent);
            }
        }
        else
        {
            GD.PrintErr("Client stream is null. Cannot start game");
        }
    }

    public void GenerateAllCards(uint playerCount)
    {
        var colors = new[] { Color.Red, Color.Blue, Color.Green, Color.Yellow };
        var cards = new Card[40 * playerCount];
        for (uint playerId = 0; playerId < playerCount; playerId++)
        {
            for (uint j = 0; j < 40; j++)
            {
                cards[j + 40 * playerId] = new Card
                {
                    PlayerId = playerId,
                    Color = colors[j / 10],
                    Number = (uint)(j % 10) + 1,
                    Gender = j % 2 == 0 ? Gender.Boy : Gender.Girl
                };
            }
        }

        CardsContext = cards;
    }

    public async Task<bool> WaitForGameStarted()
    {
        while (!isGameStarted)
        {
            Log(LogLevel.Debug, "Waiting for game to start");
            await Task.Delay(100);
        }
        SessionManager.Log(LogLevel.Information, "Game started");
        return true;
    }
}