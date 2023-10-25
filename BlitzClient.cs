#nullable enable
using Godot;
using System;
using Grpc.Net.Client;
using Proto;
public partial class Test : Node2D
{
	private GrpcChannel _channel;
	private string _username;
	private string _serverAddress;
	private Player? _player;
	private SessionService.SessionServiceClient _sessionServiceClient;
	private GameService.GameServiceClient _gameServiceClient;
	// Called when the node enters the scene tree for the first time.
	public override  void _Ready()
	{
		  _channel = GrpcChannel.ForAddress("http://localhost:50051");
		var _sessionServiceClient = new SessionService.SessionServiceClient(_channel);
		var reply =  _sessionServiceClient.StartSession(new StartSessionRq
		{
			Username = "Bob",
			FaceImageId = 1
		});
		//hello
		_player = reply;
		GD.Print(reply);


	}

	public void HandleServerGamePlayEvent(ServerEvent e)
	{
		switch (e.EventCase)
		{
			case ServerEvent.EventOneofCase.None:
				break;
			case ServerEvent.EventOneofCase.GameStateChange:
				break;
			case ServerEvent.EventOneofCase.Acknowledge:
				break;
			case ServerEvent.EventOneofCase.ServerGameStateAction:
				break;
			case ServerEvent.EventOneofCase.RequestStartGame:
				break;
			case ServerEvent.EventOneofCase.ChangeDrawRate:
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
