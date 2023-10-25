#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot.Collections;
using Color = Proto.Color;

public partial class Arena : Node2D
{
    private Vector2 _arenaSize;

    private Control? _arenaExtents;


    ///The size that a single card will take, in the same units as the ArenaSize.
    [Export] public float CardXRenderSize = 200;

    [Export] public int ProgressAmount;
    private Vector2 _gridCenter;
    private bool[,] _grid;
    private List<Deck> _decks;

    private (int, int) _currentLoc;
    private double elapsedTime = 0;
    private SessionManager.ArenaActionDelegate arenaActionHandler;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        //set arena size to the size of the screen
        this._decks = new List<Deck>(0);


        _arenaExtents = GetNodeOrNull<Control>("%ArenaSize");
        if (_arenaExtents is null)
        {
            GD.PrintErr("Could not find %ArenaSize");

            return;
        }

        _arenaSize = _arenaExtents.Size;
        _gridCenter = _arenaSize / 2;
        var gridXL = Mathf.CeilToInt(_arenaSize.X / CardXRenderSize);
        var gridYL = Mathf.CeilToInt(_arenaSize.Y / (CardXRenderSize * Deck.CardYXRatio));
        _grid = new bool[gridXL, gridYL];
        _currentLoc = (gridXL / 2, gridYL / 2);
        arenaActionHandler = new SessionManager.ArenaActionDelegate(ArenaActionEventHandler);
       
    }

    //Progress the automaton by one iteration
    //It's defined by the following rules:
    //1. Grow to the next available square that is the closest to the center of the arena
    private Vector2? Progress(uint n)
    {
        (float, (int, int))? SmallestDist((int, int) pos)
        {
            var (x, y) = pos;
            var available = new List<(int, int)>();
            if (x > 0 && !_grid[x - 1, y])
            {
                available.Add((x - 1, y));
            }

            if (x < _grid.GetLength(0) - 1 && !_grid[x + 1, y])
            {
                available.Add((x + 1, y));
            }

            if (y > 0 && !_grid[x, y - 1])
            {
                available.Add((x, y - 1));
            }

            if (y < _grid.GetLength(1) - 1 && !_grid[x, y + 1])
            {
                available.Add((x, y + 1));
            }

            //if empty, grid is full
            if (available.Count == 0)
            {
                return null;
            }

            return available.Select((pos, i) =>
                {
                    var dist_to_center = (ToGameSpace(pos) - _gridCenter).Length();
                    return (dist_to_center, pos);
                })
                .Aggregate((Mathf.Inf, (0, 0)), (acc, val) => val.Item1 < acc.Item1 ? val : acc);
        }

        var res = SmallestDist(_currentLoc);


        if (res is not null)
        {
          
            var (dist, (x, y)) = res.Value;
            _grid[x, y] = true;
            _currentLoc = (x, y);
            return ToGameSpace(_currentLoc);
        }

        return null;
    }

    //Takes a list of deck card counts, so for every deck at i, there should be decks[i] cards.
    void ArenaActionEventHandler((uint,Color)[] decks)
    {
        for (var index = 0; index < decks.Length; index++)
        {
            var (count, color) = decks[index];
            SetDeck((uint)index,count,color);
        }
    }

    void SetDeck(uint index, uint count, Color color)
    {
        if (_decks.Count<index+1)
        {
            _decks[(int)index].CardCount = (int)count;
        }
        else if (_decks.Count==index+2 && count==0)
        {
            var pos = Progress((uint)index);
            var deck = new Deck();
            if (pos != null) deck.Position = pos.Value;
            deck.Color = color;
            deck.CardCount=(int)count;
            deck.CardRenderSize = CardXRenderSize;
           _decks.Add(deck);
           AddChild(deck);
        }
    }
    private Vector2 ToGameSpace((int, int) pos)
    {
        pos = (pos.Item1 - _grid.GetLength(0) / 2, pos.Item2 - _grid.GetLength(1) / 2);
        return 1.2f * (new Vector2(pos.Item1 * CardXRenderSize * Deck.CardYXRatio,
            pos.Item2 * CardXRenderSize * Deck.CardYXRatio)) + _gridCenter;
    }

    /// <summary>
    /// Get the current length of one side of the grid given the current iteration of the automaton
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    private uint GetCurrentGridLength(uint n)
    {
        return (uint)Mathf.CeilToInt(Mathf.Sqrt(n));
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        elapsedTime += delta;
        if (elapsedTime % .5f < delta && _decks.Count < ProgressAmount)
        {
            var pos = Progress((uint)elapsedTime);
            var deck = new Deck();
            if (pos != null) deck.Position = pos.Value;
            deck.Color = (Color)GD.RandRange(0, 3);
            deck.CardCount = (int)GD.RandRange(1, 10);
            deck.CardRenderSize = CardXRenderSize;
            _decks.Add(deck);
            AddChild(deck);
        }
    }
}