using Godot;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Proto;
using Serilog;

public partial class PlayerView : Node2D
{
	private Deck blitzPile;

	private List<Deck> postPile;
	private Deck _playerHand;
	private uint _drawRate = 3;
	private uint _postPiles = 3;

	public PlayerView(uint drawRate=3, uint postPiles=3)
	{
		_drawRate = drawRate;
		_postPiles = postPiles;
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_playerHand = new Deck(DeckType.PlayerHand);
		
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	void PlayerActionEventHandler(Proto.PlayerPlay play){
	 switch (play.PlayType)
	 {
		 case PlayerPlayType.BlitzToPost:
			 break;
		 case PlayerPlayType.AvailableHandToPost:
			 break;
		 case PlayerPlayType.TransferToAvailableHand:
			 break;
		 case PlayerPlayType.ResetHand:
			 break;
		 default:
			 throw new ArgumentOutOfRangeException();
	 }	
	}
}
