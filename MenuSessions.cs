#nullable enable
using Godot;
using System;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto;

public partial class MenuSessions : Control
{
    [Export] public ItemList? SessionItemList;
    [Export] public Button? NewSessionButton;
    public Session[]? Sessions;


    private EMenuView _eMenuView;
    private SessionService.SessionServiceClient? _sessionServiceClient;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        //get session manager from autoload
        var sessionManager = GetNode<SessionManager>("/root/SessionManager");
        _sessionServiceClient = sessionManager.SessionServiceClient;

        var sessions = _sessionServiceClient?.GetActiveSessions(new Empty());
        var listItems = sessions?.Sessions;
        Sessions = listItems?.ToArray();
        if (SessionItemList != null)
        {
            if (listItems != null)
                foreach (var item in listItems)
                {
                    if (item.Players.Count == 0) continue;
                    var val = item.Players[0] + " [" + item.Id[..5] + "]";
                    SessionItemList.AddItem(val);
                    SessionManager.Log(LogLevel.Information, "Added item " + val + " to list");
                }
        }
        else
        {
            GD.Print("SessionList is null");
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }

    private void _on_bt_refresh_sessions_pressed()
    {
        if (SessionItemList != null)
        {
            var sessions = _sessionServiceClient?.GetActiveSessions(new Empty());

            var listItems = sessions?.Sessions;
            //clear the list
            SessionItemList.Clear();
            if (listItems != null)
                foreach (var item in listItems)
                {
                    if (item.Players.Count == 0) continue;
                    var val = item.Players[0] + " [" + item.Id[..5] + "]";
                    SessionItemList.AddItem(val);
                    SessionManager.Log(LogLevel.Information, "Added item " + val + " to list");
                }
        }
        else
        {
            GD.Print("SessionList is null");
        }
    }

    private void _on_bt_new_game_pressed()
    {
        GD.Print("New game");
    }
}