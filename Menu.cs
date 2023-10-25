#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Proto;

enum EMenuView
{
    AvailableSessions,
    NewSession,
    JoinSession,
    NewGameFromNewSession,
    NewGameFromJoinSession,
    GameUI,
}

public partial class Menu : Control
{
    [Export] public ItemList? SessionList;
    [Export] public PackedScene? SessionsView;
    [Export] public PackedScene? NewSessionView;
    [Export] public PackedScene? GameUiView;
    [Export] public PackedScene? NewGameView;
    private string? _sessionId;
    private SessionManager? _sessionManager;
    private Player? _player;


    private EMenuView _eMenuView;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _sessionManager = GetNode<SessionManager>("/root/SessionManager");
        //create SesssionsView scene
        var sessionsView = SessionsView?.Instantiate<MenuSessions>();
        if (sessionsView == null)
        {
            GD.PrintErr("SessionsView is null");
            return;
        }

        //connect with new session button press signal
        sessionsView.NewSessionButton!.Pressed += _OnOpenNewSessionView;
        sessionsView.SessionItemList!.ItemSelected += (id) =>
        {
            GD.Print("Selected session " + id);
            _sessionId = sessionsView.Sessions![(int)id].Id;
            TransitionScene(EMenuView.JoinSession);
        };
        AddChild(sessionsView);
    }


    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }

    private void _OnOpenNewSessionView()
    {
        GD.Print("Opening new session view");
        TransitionScene(EMenuView.NewSession);
    }

    private void TransitionScene(EMenuView eMenuView)
    {
        var screenRect = GetViewportRect();
        //move all scenes of current view
        var hidePrev = () =>
            {
                foreach (var node in GetChildren())
                {
                    if (node is not Control child)
                    {
                        GD.PrintErr("Child is not container");
                        continue;
                    }

                    var tween = child.CreateTween();
                    tween.TweenMethod(Callable.From<Vector2>((p) => { child.Position = p; }), child.Position,
                        new Vector2(0, -screenRect.End.Y), 0.5f);
                    tween.SetEase(Tween.EaseType.InOut);
                }
            }
            ;

        //add new child
        switch (eMenuView)
        {
            case EMenuView.AvailableSessions:
                var sessionsView = GetNodeOrNull<MenuSessions>("SessionsScreen");
                if (sessionsView == null)
                {
                    sessionsView = SessionsView?.Instantiate<MenuSessions>();
                    if (sessionsView == null)
                    {
                        GD.PrintErr("SessionsView is null");
                        return;
                    }

                    hidePrev();
                    AddChild(sessionsView);
                }


                //set position to be offscreen
                sessionsView!.Position = new Vector2(0, 1000);
                //tween in
                var tween = sessionsView.CreateTween();
                tween.TweenMethod(Callable.From<Vector2>((p) => { sessionsView.Position = p; }), sessionsView.Position,
                    new Vector2(0, 0), 0.5f);
                //connect with new session button press signal
                sessionsView.NewSessionButton!.Pressed += _OnOpenNewSessionView;
                break;
            case EMenuView.NewSession:
                var newSessionView = GetNodeOrNull<NewSession>("NewSession");
                if (newSessionView == null)
                {
                    newSessionView = NewSessionView?.Instantiate<NewSession>();
                    if (newSessionView == null)
                    {
                        GD.PrintErr("NewSessionView is null");
                        return;
                    }

                    hidePrev();
                    AddChild(newSessionView);
                }

                newSessionView.CreateSessionButton!.Pressed += () =>
                {
                    if (newSessionView.UsernameTE != null)
                    {
                        var text = newSessionView.UsernameTE.Text;
                        if (text is { Length: > 0 })
                        {
                            //get global session manager
                            var sessionManager = GetNode<SessionManager>("/root/SessionManager");
                            //get session service client
                            var sessionServiceClient = sessionManager.SessionServiceClient;

                            try
                            {
                                var player = sessionServiceClient?.StartSession(new StartSessionRq
                                {
                                    FaceImageId = 0,
                                    Username = text,
                                });
                                if (player == null)
                                {
                                    throw new Exception("Player is null. Could not start session");
                                }

                                var session = sessionServiceClient?.GetSession(new GetSessionRq
                                {
                                    SessionId = player.SessionId
                                });
                                _player = player;
                                TransitionScene(EMenuView.NewGameFromNewSession);
                            }
                            catch (RpcException e)
                            {
                                GD.PrintErr("Error joining session: " + e.Message);
                            }
                        }
                        else
                        {
                            GD.PrintErr("Username is null or empty");
                        }
                    }
                };
                //set position to be offscreen
                newSessionView.Position = new Vector2(0, screenRect.End.Y);
                if (newSessionView.Title != null) newSessionView.Title.Text = "new session";
                //tween in
                var tween2 = newSessionView.CreateTween();
                tween2.TweenMethod(Callable.From<Vector2>((p) => { newSessionView.Position = p; }),
                    newSessionView.Position, new Vector2(0, 0), 0.5f);
                tween2.SetEase(Tween.EaseType.InOut);
                break;
            case EMenuView.JoinSession:

            {
                var sessions = _sessionManager?.SessionServiceClient?.GetActiveSessions(new Empty());
                if (sessions == null)
                {
                    GD.PrintErr("Sessions is null");
                    return;
                }


                try
                {
                    if (_sessionId == null)
                    {
                        GD.PrintErr("No session selected");
                    }

                    var selectedSession = sessions.Sessions.First(ses => ses.Id == _sessionId);
                    var newSessionView2 = GetNodeOrNull<NewSession>("JoinSession");
                    if (newSessionView2 == null)
                    {
                        newSessionView2 = NewSessionView?.Instantiate<NewSession>();
                        if (newSessionView2 == null)
                        {
                            GD.PrintErr("NewSessionView is null");
                            return;
                        }

                        hidePrev();

                        AddChild(newSessionView2);
                    }

                    newSessionView2.Position = new Vector2(0, screenRect.End.Y);
                    newSessionView2.CreateSessionButton!.Pressed += () =>
                    {
                        if (newSessionView2.UsernameTE != null)
                        {
                            var text = newSessionView2.UsernameTE.Text;
                            if (text is { Length: > 0 })
                            {
                                //get global session manager
                                var sessionManager = GetNode<SessionManager>("/root/SessionManager");
                                //get session service client
                                var sessionServiceClient = sessionManager.SessionServiceClient;

                                try
                                {
                                    var player = sessionServiceClient?.JoinSession(new JoinSessionRq
                                    {
                                        FaceImageId = 0,
                                        Username = text,
                                        SessionId = _sessionId!
                                    });
                                    _player = player;
                                    GD.Print($"Joined session {_sessionId}");
                                    TransitionScene(EMenuView.NewGameFromJoinSession);
                                }
                                catch (RpcException e)
                                {
                                    GD.PrintErr("Error joining session: " + e.Message);
                                }
                            }
                            else
                            {
                                GD.PrintErr("Username is null or empty");
                            }
                        }
                    };

                    newSessionView2.Name = "JoinSession";
                    if (newSessionView2.Title != null)
                        newSessionView2.Title.Text = "joining <" + selectedSession.Players[0] + "> session";
                    //tween in
                    var tween3 = newSessionView2.CreateTween();
                    tween3.TweenMethod(Callable.From<Vector2>((p) => { newSessionView2.Position = p; }),
                        newSessionView2.Position, new Vector2(0, 0), 0.5f);
                    tween3.SetEase(Tween.EaseType.InOut);
                }
                catch (InvalidOperationException e)
                {
                    GD.PrintErr("Session not found");
                }

                break;
            }
            case EMenuView.GameUI:
                var gameUiView = GetNodeOrNull<GameUI>("GameUI");
                if (gameUiView == null)
                {
                    gameUiView = GameUiView?.Instantiate<GameUI>();
                    if (gameUiView == null)
                    {
                        GD.PrintErr("GameUiView is null");
                        return;
                    }

                    hidePrev();
                    AddChild(gameUiView);
                }

                //set position to be offscreen
                gameUiView.Position = new Vector2(0, screenRect.End.Y);
                //tween in
                var tween4 = gameUiView.CreateTween();
                tween4.TweenMethod(Callable.From<Vector2>((p) => { gameUiView.Position = p; }),
                    gameUiView.Position, new Vector2(0, 0), 0.5f);
                tween4.SetEase(Tween.EaseType.InOut);

                break;
            case EMenuView.NewGameFromNewSession:
                GD.Print("transition to new game");
                var newGameView = GetNodeOrNull<StartGameView>("StartGameView");
                if (newGameView == null)
                {
                    newGameView = NewGameView?.Instantiate<StartGameView>();
                    if (newGameView == null)
                    {
                        GD.PrintErr("NewGameView is null");
                        return;
                    }

                    hidePrev();
                    AddChild(newGameView);
                }

                //set position to be offscreen
                newGameView.Position = new Vector2(0, screenRect.End.Y);
                //tween in
                var tween5 = newGameView.CreateTween();
                tween5.TweenMethod(Callable.From<Vector2>((p) => { newGameView.Position = p; }),
                    newGameView.Position, new Vector2(0, 0), 0.5f);
                tween5.SetEase(Tween.EaseType.InOut);


                if (_player == null)
                {
                    GD.PrintErr("Player is null");
                    return;
                }

                //Start timer to fetch player list every second
                var _timer = new Timer();
                _timer.WaitTime = .4;
                _timer.OneShot = false;
                _timer.Timeout += () =>
                {
                    newGameView.RefreshPlayerList(_sessionManager!.SessionServiceClient?.GetSession(new GetSessionRq
                    {
                        SessionId = _player.SessionId
                    }).Players.ToList());
                };
                AddChild(_timer);
                _timer.Start();
                if (_player == null)
                {
                    GD.PrintErr("Player is null");
                    return;
                }

                if (newGameView.StartGameButton != null)
                    newGameView.StartGameButton.Pressed += async () =>
                    {
                        if (_player == null)
                        {
                            GD.PrintErr("player is null");
                            return;
                        }

                        GD.Print("Opening event stream");
                        await _sessionManager!.OpenEventStream(_player);
                        GD.Print("Starting game");
                        await _sessionManager.RequestStartGame();
                        SessionManager.Log(LogLevel.Information, "Waiting for game started event");
                        await _sessionManager.WaitForGameStarted();
                        //remove timer
                        _timer.Stop();
                        _timer.QueueFree();
                        TransitionScene(EMenuView.GameUI);
                    };

                break;
            case EMenuView.NewGameFromJoinSession:
            {
                GD.Print("transition to new game from join session");
                var newGameView2 = GetNodeOrNull<StartGameView>("StartGameView");
                if (newGameView2 == null)
                {
                    newGameView2 = NewGameView?.Instantiate<StartGameView>();
                    if (newGameView2 == null)
                    {
                        GD.PrintErr("NewGameView is null");
                        return;
                    }

                    hidePrev();
                    AddChild(newGameView2);
                }

                newGameView2.NewGameLabel!.Text = "joining game";
                newGameView2.StartGameButton!.Disabled = true;
                newGameView2.StartGameButton!.Text = "waiting...";

                //set position to be offscreen
                newGameView2.Position = new Vector2(0, screenRect.End.Y);
                //tween in
                var tween6 = newGameView2.CreateTween();
                tween6.TweenMethod(Callable.From<Vector2>((p) => { newGameView2.Position = p; }),
                    newGameView2.Position, new Vector2(0, 0), 0.5f);
                tween6.SetEase(Tween.EaseType.InOut);


                if (_player == null)
                {
                    GD.PrintErr("Player is null");
                    return;
                }

                //Start timer to fetch player list every second
                var _timer2 = new Timer();
                _timer2.WaitTime = .4;
                _timer2.OneShot = false;
                _timer2.Timeout += () =>
                {
                    var plist = _sessionManager!.SessionServiceClient?.GetSession(new GetSessionRq
                    {
                        SessionId = _player.SessionId
                    }).Players.ToList();
                    if (plist != null) newGameView2.RefreshPlayerList(plist);
                };
                AddChild(_timer2);
                _timer2.Start();
                if (_player == null)
                {
                    GD.PrintErr("Player is null");
                    return;
                }


                if (_player == null)
                {
                    GD.PrintErr("player is null");
                    return;
                }

                _sessionManager!.OpenEventStream(_player).ContinueWith(async (s) =>
                {
                    SessionManager.Log(LogLevel.Information, "Waiting for game started event");
                    var res = await _sessionManager.WaitForGameStarted();
                    SessionManager.Log(LogLevel.Information, "Game started event received");
                    _timer2.CallDeferred("Stop");
                    _timer2.QueueFree();
                    CallDeferred("TransitionScene", Variant.From(EMenuView.GameUI));
                });
            }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(eMenuView), eMenuView, null);
        }
    }
}