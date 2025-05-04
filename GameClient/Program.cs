// Program.cs (GameClient)
using System;
using LiteNetLib;
using LiteNetLib.Utils;
using System.IO;
using System.Threading;
using System.Text.Json;

<<<<<<< HEAD
namespace GameClientNamespace {
   public class Config {
	  public string ServerAddress { get; set; } = "57.128.228.75"; // default dev server
	  public int ServerPort { get; set; } = 9050;

	  public static Config Load(string filePath) {
		 try {
			string json = File.ReadAllText(filePath);
			var options = new JsonSerializerOptions {
			   PropertyNameCaseInsensitive = true
			};
			return JsonSerializer.Deserialize<Config>(json, options) ?? new Config();
		 }
		 catch (Exception e) {
			Console.WriteLine("Error loading config: " + e.Message);
			return new Config();
		 }
	  }
   }

   class GameClient : INetEventListener {
	  private NetManager _client;
	  private NetPeer _server;
	  private string? _token;
	  private string _username = "";
	  private int _lastLatency = -1;
	  private string _serverIp;
	  private int _serverPort;
	  private bool _connected = false;
	  private bool _shouldReconnect = true;

	  public GameClient(string serverIp, int serverPort) {
		 _serverIp = serverIp;
		 _serverPort = serverPort;
		 _client = new NetManager(this);
		 _client.Start();
	  }

	  public void Connect() {
		 Console.WriteLine("Attempting to connect...");
		 _client.Connect(_serverIp, _serverPort, "game_app");
	  }

	  public void PollEvents() {
		 _client.PollEvents();
	  }

	  public void OnPeerConnected(NetPeer peer) {
		 Console.WriteLine("Connected to server");
		 _server = peer;
		 _connected = true;
	  }

	  public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
		 Console.WriteLine("Disconnected: " + disconnectInfo.Reason);
		 _connected = false;
	  }

	  public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketErrorCode) {
		 Console.WriteLine("Network error: " + socketErrorCode);
	  }

	  public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
		 byte msgType = reader.GetByte();
		 switch (msgType) {
			case 1:
			   string msg = reader.GetString();
			   Console.WriteLine(msg);
			   break;
			case 3:
			   string err = reader.GetString();
			   Console.WriteLine("Error: " + err);
			   break;
			case 4:
			   _token = reader.GetString();
			   Console.WriteLine("Login successful. Token: " + _token);
			   break;
			case 5:
			   string joinUser = reader.GetString();
			   float x = reader.GetFloat();
			   float y = reader.GetFloat();
			   Console.WriteLine($"{joinUser} joined at ({x}, {y})");
			   break;
			case 6:
			   string moveUser = reader.GetString();
			   float mx = reader.GetFloat();
			   float my = reader.GetFloat();
			   Console.WriteLine($"{moveUser} moved to ({mx}, {my})");
			   break;
			case 7:
			   string leftUser = reader.GetString();
			   Console.WriteLine($"{leftUser} has left the game.");
			   break;
		 }
		 reader.Recycle();
	  }

	  public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

	  public void OnConnectionRequest(ConnectionRequest request) {
		 request.AcceptIfKey("game_app");
	  }

	  public void OnNetworkLatencyUpdate(NetPeer peer, int latency) {
		 if (_lastLatency == -1 || Math.Abs(latency - _lastLatency) >= 30) {
			_lastLatency = latency;
			Console.WriteLine($"Latency: {latency} ms");
		 }
	  }

	  public void Login(string username) {
		 _username = username;
		 var writer = new NetDataWriter();
		 writer.Put((byte)0);
		 writer.Put(username);
		 _server.Send(writer, DeliveryMethod.ReliableOrdered);
	  }

	  public void Logout() {
		 if (_token == null) return;
		 var writer = new NetDataWriter();
		 writer.Put((byte)8);
		 writer.Put(_token);
		 _server.Send(writer, DeliveryMethod.ReliableOrdered);
		 Console.WriteLine("Logged out.");
		 _token = null;
	  }

	  public void SendChat(string message) {
		 if (_token == null) return;
		 var writer = new NetDataWriter();
		 writer.Put((byte)1);
		 writer.Put(_token);
		 writer.Put(message);
		 _server.Send(writer, DeliveryMethod.ReliableOrdered);
	  }

	  public void SendPosition(float x, float y) {
		 if (_token == null) return;
		 var writer = new NetDataWriter();
		 writer.Put((byte)2);
		 writer.Put(_token);
		 writer.Put(x);
		 writer.Put(y);
		 _server.Send(writer, DeliveryMethod.Sequenced);
	  }

	  public void RunEventLoop() {
		 while (_shouldReconnect) {
			PollEvents();

			if (!_connected) {
			   Connect();
			   Thread.Sleep(2000); // wait before retrying
			}

			Thread.Sleep(15);
		 }
	  }
   }

   class Program {


	  static void Main(string[] args) {
		 string configPath = "config.json";
		 Config config = Config.Load(configPath);

		 Console.WriteLine($"Loaded config: {config.ServerAddress}:{config.ServerPort}");  // <-- добавь это

		 var client = new GameClient(config.ServerAddress, config.ServerPort);
		 var clientThread = new Thread(client.RunEventLoop);
		 clientThread.Start();

		 Console.WriteLine("Type 'login <username>', 'logout', 'chat <message>', or 'pos <x> <y>' to interact.");

		 while (true) {
			string? input = Console.ReadLine();
			if (input == null) continue;

			var parts = input.Split(' ', 2);
			string cmd = parts[0].ToLower();

			switch (cmd) {
			   case "login":
				  if (parts.Length > 1) client.Login(parts[1]);
				  else Console.WriteLine("Usage: login <username>");
				  break;
			   case "logout":
				  client.Logout();
				  break;
			   case "chat":
				  if (parts.Length > 1) client.SendChat(parts[1]);
				  else Console.WriteLine("Usage: chat <message>");
				  break;
			   case "pos":
				  var coords = parts.Length > 1 ? parts[1].Split(' ') : Array.Empty<string>();
				  if (coords.Length == 2 &&
					  float.TryParse(coords[0], out float x) &&
					  float.TryParse(coords[1], out float y)) {
					 client.SendPosition(x, y);
				  }
				  else {
					 Console.WriteLine("Usage: pos <x> <y>");
				  }
				  break;
			}
		 }
	  }
   }

}
