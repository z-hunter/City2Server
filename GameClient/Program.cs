using System;
using System.Net;
using LiteNetLib;
using LiteNetLib.Utils;
using System.IO;

// Простой клиент игры с авторизацией и отправкой данных
class GameClient : INetEventListener
{
    private NetManager _client;              // Менеджер клиента
    private NetPeer? _serverPeer;            // Ссылка на подключённый сервер
    private NetDataWriter _writer;           // Для отправки данных
    private string _username = "";           // Имя пользователя (логин)

    public GameClient()
    {
        _client = new NetManager(this);
        _writer = new NetDataWriter();
    }

    public void Start()
    {
        _client.Start();
        _client.Connect("localhost", 9050, "game_app"); // Подключение к серверу
    }

    public void PollEvents()
    {
        _client.PollEvents();
    }

    public void Stop()
    {
        _client.Stop();
    }

    // Отправка логина серверу
    public void SendLogin(string username)
    {
        _username = username;
        _writer.Reset();
        _writer.Put((byte)0); // Тип сообщения: логин
        _writer.Put(username);
        _serverPeer?.Send(_writer, DeliveryMethod.ReliableOrdered);
    }

    // Отправка сообщения чата
    public void SendChat(string text)
    {
        if (_serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected)
        {
            _writer.Reset();
            _writer.Put((byte)1); // Тип: чат
            _writer.Put(text);
            _serverPeer.Send(_writer, DeliveryMethod.ReliableOrdered);
        }
    }

    // Отправка позиции игрока
    public void SendPosition(float x, float y)
    {
        if (_serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected)
        {
            _writer.Reset();
            _writer.Put((byte)2); // Тип: позиция
            _writer.Put(x);
            _writer.Put(y);
            _serverPeer.Send(_writer, DeliveryMethod.Sequenced);
        }
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine("Connected to server. Please login with 'login <name>'");
        _serverPeer = peer;
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Console.WriteLine("Disconnected from server. Reason: " + disconnectInfo.Reason);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        byte msgType = reader.GetByte();

        switch (msgType)
        {
            case 1:
                string chatReply = reader.GetString();
                Console.WriteLine("[Server replied]: " + chatReply);
                break;
            case 2:
                float x = reader.GetFloat();
                float y = reader.GetFloat();
                Console.WriteLine($"[Server echoed position]: x={x}, y={y}");
                break;
            case 3:
                string serverMsg = reader.GetString();
                Console.WriteLine("[Server]: " + serverMsg);
                if (serverMsg == "Invalid login")
                {
                    Console.WriteLine("Login failed. Please try another username.");
                }
                break;
        }

        reader.Recycle();
    }

    public void OnNetworkError(IPEndPoint endPoint, System.Net.Sockets.SocketError socketErrorCode)
    {
        Console.WriteLine("Network error: " + socketErrorCode);
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.AcceptIfKey("game_app");
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    static void Main()
    {
        GameClient client = new GameClient();
        client.Start();

        Console.WriteLine("Client started. Type 'login <name>', 'chat <msg>', 'pos x y'. Type 'exit' to quit.");

        while (true)
        {
            client.PollEvents();

            if (Console.KeyAvailable)
            {
                string line = Console.ReadLine();
                if (line == null) continue;

                if (line.ToLower() == "exit")
                    break;

                if (line.StartsWith("login "))
                {
                    string name = line.Substring(6).Trim();
                    client.SendLogin(name);
                }
                else if (line.StartsWith("chat "))
                {
                    string msg = line.Substring(5);
                    client.SendChat(msg);
                }
                else if (line.StartsWith("pos "))
                {
                    string[] parts = line.Split(' ');
                    if (parts.Length == 3 && float.TryParse(parts[1], out float x) && float.TryParse(parts[2], out float y))
                        client.SendPosition(x, y);
                    else
                        Console.WriteLine("Usage: pos 12.5 34.7");
                }
                else
                {
                    Console.WriteLine("Unknown command");
                }
            }

            System.Threading.Thread.Sleep(15);
        }

        client.Stop();
    }
}
