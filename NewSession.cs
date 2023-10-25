#nullable enable
using Godot;
using System;
using Proto;

public partial class NewSession : Control
{
	// Called when the node enters the scene tree for the first time.
	[Export]public Label? Title;
	[Export] public TextEdit? UsernameTE;
	[Export] public Button? CreateSessionButton;
	
	public override void _Ready()
	{
		if (CreateSessionButton != null) CreateSessionButton.Pressed += _on_bt_create_session_pressed;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _on_bt_create_session_pressed()
	{
		
	}
	
}
