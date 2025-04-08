using System;
using System.Net;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using System.IO;

class GameServer : INetEventListener
{
    private NetManager _server;  // Сетевой менеджер
    private Dictionary<NetPeer, string> _authenticatedPeers = new();  // Сопоставление peer -> имя пользователя
    private Dictionary<string, PlayerData> _users = new();  // Хранилище пользовательских данных
    private const string UserDataFile = "users.json";  // Имя файла для хранения данных пользователей

    public GameServer()
    {
        _server = new NetManager(this);  // Инициализация сервера
        LoadUserData();  // Загрузка данных пользователей из файла
    }

    public void Start()
    {
        _server.Start(9050);  // Запуск сервера на порту 9050
        Console.WriteLine("Server started on port 9050");
    }

    public void PollEvents()
    {
        _server.PollEvents();  // Обработка сетевых событий
    }

    public void Stop()
    {
        SaveUserData();  // Сохраняем данные пользователей
        _server.Stop();   // Останавливаем сервер
    }

    private void LoadUserData()
    {
        if (File.Exists(UserDataFile))
        {
            string json = File.ReadAllText(UserDataFile);
            _users = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, PlayerData>>(json) ?? new();
        }
    }

    private void SaveUserData()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(_users, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(UserDataFile, json);
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine("Client connected: " + peer.Address + ":" + peer.Port);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Console.WriteLine("Client disconnected: " + peer.Address + ":" + peer.Port);
        _authenticatedPeers.Remove(peer);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        byte msgType = reader.GetByte();  // Чтение типа сообщения

        switch (msgType)
        {
            case 0: // Логин
                string username = reader.GetString();
                Console.WriteLine("Login attempt: " + username);

                if (_users.ContainsKey(username))
                {
                    // Проверка: уже ли кто-то подключён с этим именем
                    if (_authenticatedPeers.ContainsValue(username))
                    {
                        Console.WriteLine("Duplicate login attempt: " + username);
                        var dupWriter = new NetDataWriter();
                        dupWriter.Put((byte)3);  // Тип сообщения: ошибка логина
                        dupWriter.Put("User already logged in");
                        peer.Send(dupWriter, DeliveryMethod.ReliableOrdered);
                        break;
                    }

                    _authenticatedPeers[peer] = username;
                    Console.WriteLine("Login successful: " + username);
                }
                else
                {
                    Console.WriteLine("Login failed: " + username);
                    var writer = new NetDataWriter();
                    writer.Put((byte)3);  // Тип сообщения: ошибка логина
                    writer.Put("Invalid login");
                    peer.Send(writer, DeliveryMethod.ReliableOrdered);
                }
                break;

            case 1: // Сообщение чата
                if (_authenticatedPeers.TryGetValue(peer, out string user))
                {
                    string msg = reader.GetString();
                    Console.WriteLine(user + " says: " + msg);

                    var reply = new NetDataWriter();
                    reply.Put((byte)1);  // Ответ типа "чат"
                    reply.Put("Echo: " + msg);
                    peer.Send(reply, DeliveryMethod.ReliableOrdered);
                }
                break;

            case 2: // Позиция игрока
                if (_authenticatedPeers.TryGetValue(peer, out string uname))
                {
                    float x = reader.GetFloat();
                    float y = reader.GetFloat();
                    Console.WriteLine(uname + " moved to x=" + x + ", y=" + y);

                    var response = new NetDataWriter();
                    response.Put((byte)2);  // Ответ типа "позиция"
                    response.Put(x);
                    response.Put(y);
                    peer.Send(response, DeliveryMethod.Sequenced);

                    // Сохраняем позицию игрока
                    _users[uname].LastX = x;
                    _users[uname].LastY = y;
                }
                break;
        }

        reader.Recycle();  // Освобождаем ресурсы
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.AcceptIfKey("game_app");  // Принимаем соединение по ключу
    }

    public void OnNetworkError(IPEndPoint endPoint, System.Net.Sockets.SocketError socketErrorCode)
    {
        Console.WriteLine("Network error: " + socketErrorCode);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    static void Main()
    {
        GameServer server = new GameServer();
        server.Start();

        Console.WriteLine("Press Enter to quit.");
        while (!Console.KeyAvailable)
        {
            server.PollEvents();
            System.Threading.Thread.Sleep(15);
        }

        server.Stop();
    }
}

public class PlayerData
{
    public float LastX { get; set; }
    public float LastY { get; set; }
}
