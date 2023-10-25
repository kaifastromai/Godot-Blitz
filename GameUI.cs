#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Proto;


enum EStateType
{
    KBlitz,
    KEnd,
    KPost,
    KHand,
    KReshuffle,
    KCycle,
}

enum ECommand
{
    Hand,
    ResetHand,
    TransferToAvailable,
    Arena,
    CallBlitz,
    Post,
    Blitz,
    Available,
}

struct Command
{
    public ECommand CommandType;
    public uint? Index;

    public Command(ECommand commandType, uint? index)
    {
        this.CommandType = commandType;
        this.Index = index;
    }
}

enum EPile
{
    Arena,
    Post,
    Available,
    Blitz
}

abstract class State
{
    protected EStateType StateType;
    public abstract State? Progress(Command c);

    public EStateType GetStateType()
    {
        return StateType;
    }
}

class InitState : State
{
    public override State? Progress(Command c)
    {
        State res;
        switch (c.CommandType)
        {
            case ECommand.Hand:
                res = new HandState();
                break;
            case ECommand.ResetHand:
                break;
            case ECommand.TransferToAvailable:
                break;
            case ECommand.Arena:
                break;
            case ECommand.Post:
                if (c.Index == null)
                {
                    SessionManager.Log(LogLevel.Error, "Post command received without index");
                    return new ErrorState("Post command received without index");
                }

                return new FromPostState(c.Index.Value);
            case ECommand.Blitz:
            case ECommand.Available:
            default:
                throw new ArgumentOutOfRangeException();
        }

        return res;
    }
}

class EndState : State
{
    private Proto.Play _play;

    public EndState(Play play)
    {
        this.StateType = EStateType.KEnd;
        this._play = play;
    }

    public override State? Progress(Command c)
    {
        return null;
    }
}


enum HandPlayType
{
    BlitzPile,
    PostPile,
    AvailablePile
}

class HandState : State
{
    public HandState()
    {
        this.StateType = EStateType.KHand;
    }

    public override State? Progress(Command c)
    {
        switch (c.CommandType)
        {
            case ECommand.Hand:
                SessionManager.Log(LogLevel.Error, "Hand command received in HandState");
                return new ErrorState("Hand command received in HandState");
                break;
            case ECommand.ResetHand:
                return new ResetHandState();
                break;
            case ECommand.TransferToAvailable:
                return new TransferToAvailableState();
                break;
            case ECommand.Arena:
                SessionManager.Log(LogLevel.Error, "Arena command received in HandState");
                return new ErrorState("Cannot play arena from hand state.");
                break;
            
            case ECommand.CallBlitz:
                return new CallBlitzState();
            case ECommand.Post:
                return new FromPostState(c.Index.Value);
            case ECommand.Blitz:
                return new FromBlitzPileState(EPile.Blitz);
            case ECommand.Available:
                return new FromAvailableState();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

class FromAvailableState : State
{
    public override State? Progress(Command c)
    {
        if (c.CommandType == ECommand.CallBlitz)
        {
            return new CallBlitzState();
        }

        switch (c.CommandType)
        {
            case ECommand.Hand:
                return ErrorState.ReturnErrorState("Cannot move into hand");
                break;
            case ECommand.ResetHand:
                return ErrorState.ReturnErrorState("Cannot reset in middle of play");
                break;
            case ECommand.TransferToAvailable:
                return ErrorState.ReturnErrorState("Cannot transfer in middle of play");
                break;
            case ECommand.Arena:
                return new ArenaState(ArenaState.EFrom.Available, 0);
                break;
            case ECommand.CallBlitz:
                return new CallBlitzState();
                break;
            case ECommand.Post:
                return new ToPostState(c.Index.Value, EPile.Available);
                break;
            case ECommand.Blitz:
                return ErrorState.ReturnErrorState("Cannot ever move to blitz pile");
                break;
            case ECommand.Available:
                return ErrorState.ReturnErrorState("Cannot ever move to available pile");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        {
            
        }
    }
}
internal class TransferToAvailableState : State
{
    public override State? Progress(Command c)
    {
        if (c.CommandType == ECommand.CallBlitz)
        {
            return new CallBlitzState();
        }

        return new EndState(new Play()
        {
            PlayerPlay = new PlayerPlay() { PlayType = PlayerPlayType.TransferToAvailableHand }
        });
    }
}

class ResetHandState : State
{
    public ResetHandState()
    {
    }

    public override State? Progress(Command c)
    {
        return new EndState(new Play
        {
            PlayerPlay = { PlayType = PlayerPlayType.ResetHand, PostIndex = 0 }
        });
    }
}

class ErrorState : State
{
    string _message;

    public ErrorState(string message)
    {
        this._message = message;
    }

    public override State? Progress(Command c)
    {
        throw new NotImplementedException();
    }

    public static State ReturnErrorState(string message)
    {
        SessionManager.Log(LogLevel.Error, message);
        return new ErrorState(message);
    }
}

//Cancel building the current play
class Cancel : State
{
    string _message;

    public Cancel(string message)
    {
        this._message = message;
    }

    public override State? Progress(Command c)
    {
        return null;
    }
}

class ArenaState : State
{
    private uint _fromIndex;
    uint _toIndex;

    public enum EFrom
    {
        Post,
        Blitz,
        Available
    }

    private EFrom _from;
    private ArenaPlay _play;

    public ArenaState(EFrom from, uint fromIndex)
    {
        this._from = from;
    }

    public override State? Progress(Command c)
    {
        SessionManager.Log(LogLevel.Information,"Arena state with command " + c.CommandType+" and value"+c.Index);
        var play = _play = new Proto.ArenaPlay
        {
            PlayType = _from switch
            {
                EFrom.Post => ArenaPlayType.FromPost,
                EFrom.Blitz => ArenaPlayType.FromBlitz,
                EFrom.Available => ArenaPlayType.FromAvailableHand,
                _ => throw new ArgumentOutOfRangeException(nameof(_from), _from, null)
            },
            FromIndex = _fromIndex,
            ToIndex = c.Index.Value
        };
        return new EndState(new Play()
        {
            ArenaPlay = play
        });
    }
}

class FromPostState : State
{
    private readonly uint _postIndex;

    public FromPostState(uint postIndex)
    {
        this._postIndex = postIndex;
    }

    public override State? Progress(Command c)
    {
        SessionManager.Log(LogLevel.Information,"From post state with command " + c.CommandType+" and value"+c.Index);
        switch (c.CommandType)
        {
            case ECommand.Hand:
                SessionManager.Log(LogLevel.Error, "Cannot move from post to hand");
                return new ErrorState("Cannot move from post to hand");
                break;
            case ECommand.ResetHand:
                SessionManager.Log(LogLevel.Error, "Cannot reset hand in middle of play");
                return new ErrorState("Cannot reset in middle of play");
                break;
            case ECommand.TransferToAvailable:
                SessionManager.Log(LogLevel.Error, "Cannot transfer to available in middle of play");
                return new ErrorState("Cannot transfer to available in middle of play");
                break;
            case ECommand.Arena:
                if (c.Index != null) return new ArenaState(ArenaState.EFrom.Post, _postIndex);
                throw new ArgumentOutOfRangeException(nameof(c), "Arena command received without index");
                break;
            case ECommand.Post:
                SessionManager.Log(LogLevel.Error, "Cannot move from post to post");
                return new ErrorState("Cannot move from post to post");
                break;
            case ECommand.Blitz:
                SessionManager.Log(LogLevel.Error, "Cannot move from post to blitz");
                return new ErrorState("Cannot move from post to blitz");
                break;
            case ECommand.Available:
                SessionManager.Log(LogLevel.Error, "Cannot move cards into the available pile");
                return new ErrorState("Cannot move cards into the available pile");
            case ECommand.CallBlitz:
                return new CallBlitzState();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

class CallBlitzState : State
{
    public override State? Progress(Command c)
    {
        SessionManager.Log(LogLevel.Information,"Call blitz state with command " + c.CommandType+" and value"+c.Index);
        return new EndState(new Play()
        {
            CallBlitz = { }
        });
    }
}

class ToPostState : State
{
    private readonly uint _postIndex;
    private readonly EPile _fromPile;

    public ToPostState(uint postIndex, EPile fromPile)
    {
        this._fromPile = fromPile;
        this._postIndex = postIndex;
    }

    public override State? Progress(Command c)
    {
        SessionManager.Log(LogLevel.Information,"Arena pile state with command " + c.CommandType+" and value"+c.Index);
        if (c.CommandType == ECommand.CallBlitz)
        {
            return new CallBlitzState();
        }

        PlayerPlayType playType;
        switch (_fromPile)
        {
            case EPile.Arena:
                return ErrorState.ReturnErrorState("Cannot move from arena to post");
                break;
            case EPile.Post:

                return ErrorState.ReturnErrorState("Cannot move from post to post");
                break;
            case EPile.Available:
                playType = PlayerPlayType.AvailableHandToPost;
                break;
            case EPile.Blitz:
                playType = PlayerPlayType.BlitzToPost;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return new EndState(new Play
        {
            PlayerPlay = { PlayType = playType, PostIndex = _postIndex, }
        });
    }
}

class FromBlitzPileState : State
{
    Play _play;
    private EPile toPile;

    public FromBlitzPileState(EPile toPile)
    {
        this.toPile = toPile;
        StateType = EStateType.KBlitz;
    }

    public override State? Progress(Command c)
    {
        SessionManager.Log(LogLevel.Information,"Blitz pile state with command " + c.CommandType+" and value"+c.Index);
        switch (c.CommandType)
        {
            case ECommand.Hand:
                return ErrorState.ReturnErrorState("Cannot ever move to hand");
                break;
            case ECommand.ResetHand:
                return ErrorState.ReturnErrorState("Cannot reset in the middle of a play");
                break;
            case ECommand.TransferToAvailable:
                return ErrorState.ReturnErrorState("Cannot transfer in the middle of a play");
                break;
            case ECommand.Arena:
                return new ArenaState(ArenaState.EFrom.Blitz, 0);
                break;
            case ECommand.CallBlitz:
                return new CallBlitzState();
                break;
            case ECommand.Post:
                return new ToPostState(0, EPile.Blitz);
                break;
            case ECommand.Blitz:
                return ErrorState.ReturnErrorState("Cannot move from blitz to blitz");
                break;
            case ECommand.Available:
                return ErrorState.ReturnErrorState("Cannot move from blitz to available");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

//Primary point of game input handling
public partial class GameUI : Control
{
    State _state = new InitState();
    public Arena _arena;
    public List<Deck> _postPiles;
    public Deck blitzPile;
    public Deck availablePile;
    public Deck hand;
    private uint _drawRate;
    private uint _postPilesCount;
    //index of the pile that is currently selected, if any
    private uint? selectedPileIndex;
    private EPile? selectedPile;

    public GameUI(uint drawRate=3,uint postPilesCount=3)
    {
        this._drawRate = drawRate;
        this._postPilesCount = postPilesCount;
    }
    //used when generating keyboard shortcuts for the decks. Keys that are earlier in the array, have greater preferences
    public static readonly System.Collections.Immutable.ImmutableArray<char> KeyPriorityList = new ImmutableArray<char>
    {
        'j', 'k', 'h', 'l', ';', 'f', 'd', 's', 'e', 'w', 'g', 'a', 'r', 'u', 'i', 'o', 'p', 'n', 'b', 'v', 'm'
    };

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        GD.Print("Game ui ready");
    }

    public override void _Input(InputEvent @event)
    {
        //get the physical key that is currently being pressed, if any
        if (@event is InputEventKey keyEvent)
        {
            if (keyEvent.Pressed)
            {
                //check if space bar was pressed
                if (keyEvent.PhysicalKeycode == Key.Space)
                {
                    _state = _state.Progress(new Command(ECommand.CallBlitz, null))!;
                }

                var keyChar = GetKeyString(keyEvent.KeyLabel);
                if (keyChar is null) return;

                switch (_state)
                {
                    case ArenaState arenaState:
                        
                        break;
                    case FromBlitzPileState blitzPileState:
                        break;
                    case CallBlitzState callBlitzState:
                        break;
                    case Cancel cancel:
                        break;
                    case EndState endState:
                        break;
                    case ErrorState errorState:
                        break;
                    case FromPostState fromPostState:
                        break;
                    case HandState handState:
                        if (selectedPileIndex is not null && selectedPile is not null)
                        {
                            switch (selectedPile)
                            {
                                case EPile.Arena:
                                    SessionManager.Log(LogLevel.Error,"Cannot play arena from hand state");
                                    break;
                                case EPile.Post:
                                    _state = _state.Progress(new Command(ECommand.Post, selectedPileIndex))!;
                                    break;
                                case EPile.Available:
                                    _state = _state.Progress(new Command(ECommand.Available, selectedPileIndex))!;
                                    break;
                                case EPile.Blitz:
                                    _state = _state.Progress(new Command(ECommand.Blitz, selectedPileIndex))!;
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            } 
                        }
                        break;
                    //can try to call blitz any time
                    case InitState:
                        switch (keyChar.Value)
                        {
                            case 'j':
                                _state = _state.Progress(new Command(ECommand.Hand, null))!;
                                break;
                            case 'k':
                                _state = _state.Progress(new Command(ECommand.TransferToAvailable, null))!;
                                break;
                            case 'h':
                                _state = _state.Progress(new Command(ECommand.ResetHand, null))!;
                                break;
                        }

                        break;
                    case ResetHandState resetHandState:
                        break;
                    case ToPostState toPostState:
                        break;
                    case TransferToAvailableState transferToAvailableState:
                        break;
                }
            }
        }
    }

    char? GetKeyString(Key key)
    {
        var keyString = OS.GetKeycodeString(key);
        if (keyString is not null && keyString.Length == 1)
        {
            return keyString.ToLower()[0];
        }

        return null;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}