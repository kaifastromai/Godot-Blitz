#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using Proto;

public partial class StartGameView : Control
{
	 public ItemList? PlayerItemList;
	 public Button? StartGameButton;
	 public Button? RefreshPlayerListButton;
	 public Label? NewGameLabel;
	 SessionManager _sessionManager;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{

		PlayerItemList=GetNode<ItemList>("%PlayersItemList");
		StartGameButton=GetNode<Button>("%BT_StartGame");
		NewGameLabel=GetNode<Label>("%NewGameLabel");
		_sessionManager = GetNode<SessionManager>("/root/SessionManager");
		

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	public void RefreshPlayerList(List<string>? playerList)
	{
		PlayerItemList.Clear();
		foreach (var player in playerList)
		{
			PlayerItemList.AddItem(player);
		}
	}

}
