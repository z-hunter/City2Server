using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
namespace GameClientNamespace;

class GameClient : INetEventListener
{
    private NetManager _client;
    private NetPeer _serverPeer;
    private string _username;
    private string _token;
    private bool _isRunning;

    public GameClient()
    {
        _client = new NetManager(this);
    }

    public void Start(string ipAddress)
    {
        _client.Start();
        _serverPeer = _client.Connect(ipAddress, 9050, "game_app");
        Console.WriteLine("Connecting to server...");
    }

    public void PollEvents()
    {
        _client.PollEvents();
    }

    public void Stop()
    {
        _client.Stop();
    }

    // This method handles all incoming server messages
    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        byte msgType = reader.GetByte();

        switch (msgType)
        {
            case 4: HandleLoginSuccess(reader, peer); break;
            case 1: HandleChat(reader); break;
            case 2: HandlePositionUpdate(reader); break;
            case 5: HandlePlayerList(reader); break;
            case 6: HandlePositionBroadcast(reader); break;
            case 7: HandlePlayerExit(reader); break;
        }

        reader.Recycle();
    }

    private void HandleLoginSuccess(NetPacketReader reader, NetPeer peer)
    {
        _token = reader.GetString();
        Console.WriteLine("Login successful, token: " + _token);
    }

    private void HandleChat(NetPacketReader reader)
    {
        string message = reader.GetString();
        Console.WriteLine("Chat message received: " + message);
    }

    private void HandlePositionUpdate(NetPacketReader reader)
    {
        float x = reader.GetFloat();
        float y = reader.GetFloat();
        Console.WriteLine($"Received position update: x={x}, y={y}");
    }

    private void HandlePlayerList(NetPacketReader reader)
    {
        Console.WriteLine("Received player list:");
        while (reader.AvailableBytes > 0)
        {
            string username = reader.GetString();
            float x = reader.GetFloat();
            float y = reader.GetFloat();
            Console.WriteLine($"Player {username} at position x={x}, y={y}");
        }
    }

    private void HandlePositionBroadcast(NetPacketReader reader)
    {
        string username = reader.GetString();
        float x = reader.GetFloat();
        float y = reader.GetFloat();
        Console.WriteLine($"Position of player {username} updated: x={x}, y={y}");
    }

    private void HandlePlayerExit(NetPacketReader reader)
    {
        string username = reader.GetString();
        Console.WriteLine($"Player {username} has disconnected");
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine("Connected to server.");
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Console.WriteLine("Disconnected from server.");
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.AcceptIfKey("game_app");
    }

    public void OnNetworkError(IPEndPoint endPoint, System.Net.Sockets.SocketError socketErrorCode)
    {
        Console.WriteLine("Network error: " + socketErrorCode);
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

    private void Login(string username)
    {
        var writer = new NetDataWriter();
        writer.Put((byte)0);  // Message type for login
        writer.Put(username);
        _serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
        Console.WriteLine("Login request sent for: " + username);
    }

    private void SendChatMessage(string message)
    {
        var writer = new NetDataWriter();
        writer.Put((byte)1);  // Message type for chat
        writer.Put(_token);
        writer.Put(message);
        _serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void UpdatePosition(float x, float y)
    {
        var writer = new NetDataWriter();
        writer.Put((byte)2);  // Message type for position update
        writer.Put(_token);
        writer.Put(x);
        writer.Put(y);
        _serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    static void Main(string[] args)
    {
        GameClient client = new GameClient();
        client.Start("127.0.0.1");

        Console.WriteLine("Press Enter to quit.");
        bool isRunning = true;

        // Run polling in a separate task (background thread)
        Task.Run(() =>
        {
            while (isRunning)
            {
                client.PollEvents();
                Thread.Sleep(15);
            }
        });

        while (isRunning)
        {
            Console.WriteLine("Enter a command (login <username>, pos <x y>, chat <message>, quit): ");
            string command = Console.ReadLine();

            var commandParts = command.Split(' ');
            switch (commandParts[0])
            {
                case "login":
                    if (commandParts.Length > 1)
                    {
                        string username = commandParts[1];
                        client.Login(username);
                    }
                    else
                    {
                        Console.WriteLine("Usage: login <username>");
                    }
                    break;

                case "pos":
                    if (commandParts.Length == 3 && float.TryParse(commandParts[1], out float x) && float.TryParse(commandParts[2], out float y))
                    {
                        client.UpdatePosition(x, y);
                    }
                    else
                    {
                        Console.WriteLine("Usage: pos <x> <y>");
                    }
                    break;

                case "chat":
                    if (commandParts.Length > 1)
                    {
                        string message = string.Join(" ", commandParts, 1, commandParts.Length - 1);
                        client.SendChatMessage(message);
                    }
                    else
                    {
                        Console.WriteLine("Usage: chat <message>");
                    }
                    break;

                case "quit":
                    isRunning = false;
                    break;

                default:
                    Console.WriteLine("Unknown command.");
                    break;
            }
        }

        client.Stop();
    }
}
