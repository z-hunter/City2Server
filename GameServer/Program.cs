// GameServer.cs Ver. 0.0.1
using System;
using System.Net;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using System.IO;

class GameServer : INetEventListener
{
    private NetManager _server;
    private Dictionary<NetPeer, PlayerSession> _sessions = new();
    private Dictionary<string, GamePlayer> _users = new();
    private const string UserDataFile = "users.json";
    private const int ServerPort = 9050;

    public GameServer()
    {
        _server = new NetManager(this);
        LoadUserData();
    }

    public void Start()
    {
        _server.Start(ServerPort);
        Console.WriteLine($"Server started on port {ServerPort}");
    }

    public void PollEvents()
    {
        _server.PollEvents();
    }

    public void Stop()
    {
        SaveUserData();
        _server.Stop();
    }

    private void LoadUserData()
    {
        if (File.Exists(UserDataFile))
        {
            string json = File.ReadAllText(UserDataFile);
            _users = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, GamePlayer>>(json) ?? new();
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
        if (_sessions.TryGetValue(peer, out var session))
        {
            BroadcastPlayerLeft(session.Username);
            _sessions.Remove(peer);
        }
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        byte msgType = reader.GetByte();

        switch (msgType)
        {
            case 0: HandleLogin(reader, peer); break;
            case 1: HandleChat(reader, peer); break;
            case 2: HandlePosition(reader, peer); break;
        }

        reader.Recycle();
    }

    private void HandleLogin(NetPacketReader reader, NetPeer peer)
    {
        string username = reader.GetString();
        Console.WriteLine("Login attempt: " + username);

        if (_users.ContainsKey(username))
        {
            if (IsUserLoggedIn(username))
            {
                Console.WriteLine("Duplicate login attempt: " + username);
                var dupWriter = new NetDataWriter();
                dupWriter.Put((byte)3);
                dupWriter.Put("User already logged in");
                peer.Send(dupWriter, DeliveryMethod.ReliableOrdered);
                return;
            }

            string token = Guid.NewGuid().ToString();
            var session = new PlayerSession(username, token, peer);
            _sessions[peer] = session;

            Console.WriteLine("Login successful: " + username);

            // Send login success with token
            var loginSuccess = new NetDataWriter();
            loginSuccess.Put((byte)4);
            loginSuccess.Put(token);
            peer.Send(loginSuccess, DeliveryMethod.ReliableOrdered);

            // Send existing players to the newly logged in player
            foreach (var s in _sessions.Values)
            {
                if (s.Peer != peer)
                {
                    var existing = new NetDataWriter();
                    existing.Put((byte)5);
                    existing.Put(s.Username);
                    existing.Put(_users[s.Username].LastX);
                    existing.Put(_users[s.Username].LastY);
                    peer.Send(existing, DeliveryMethod.ReliableOrdered);
                }
            }

            // Notify others about the new player
            var newJoin = new NetDataWriter();
            newJoin.Put((byte)5);
            newJoin.Put(username);
            newJoin.Put(_users[username].LastX);
            newJoin.Put(_users[username].LastY);

            foreach (var s in _sessions.Values)
            {
                if (s.Peer != peer)
                    s.Peer.Send(newJoin, DeliveryMethod.ReliableOrdered);
            }
        }
        else
        {
            Console.WriteLine("Login failed: " + username);
            var writer = new NetDataWriter();
            writer.Put((byte)3);
            writer.Put("Invalid login");
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    private void HandleChat(NetPacketReader reader, NetPeer peer)
    {
        string token = reader.GetString();
        string msg = reader.GetString();
        var session = GetSessionByToken(token);

        if (session != null)
        {
            Console.WriteLine(session.Username + " says: " + msg);

            foreach (var s in _sessions.Values)
            {
                var reply = new NetDataWriter();
                reply.Put((byte)1);
                reply.Put(session.Username + ": " + msg);
                s.Peer.Send(reply, DeliveryMethod.ReliableOrdered);
            }
        }
        else
        {
            var err = new NetDataWriter();
            err.Put((byte)3);
            err.Put("Unauthorized");
            peer.Send(err, DeliveryMethod.ReliableOrdered);
        }
    }

    private void HandlePosition(NetPacketReader reader, NetPeer peer)
    {
        string token = reader.GetString();
        float x = reader.GetFloat();
        float y = reader.GetFloat();
        var session = GetSessionByToken(token);

        if (session != null)
        {
            string user = session.Username;
            Console.WriteLine(user + " moved to x=" + x + ", y=" + y);

            _users[user].LastX = x;
            _users[user].LastY = y;

            // Notify others
            foreach (var s in _sessions.Values)
            {
                var move = new NetDataWriter();
                move.Put((byte)6);
                move.Put(user);
                move.Put(x);
                move.Put(y);
                s.Peer.Send(move, DeliveryMethod.Sequenced);
            }
        }
        else
        {
            var err = new NetDataWriter();
            err.Put((byte)3);
            err.Put("Unauthorized");
            peer.Send(err, DeliveryMethod.ReliableOrdered);
        }
    }

    private void BroadcastPlayerLeft(string username)
    {
        var leave = new NetDataWriter();
        leave.Put((byte)7);
        leave.Put(username);

        foreach (var s in _sessions.Values)
        {
            s.Peer.Send(leave, DeliveryMethod.ReliableOrdered);
        }
    }

    private PlayerSession? GetSessionByToken(string token)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Token == token)
                return session;
        }
        return null;
    }

    private bool IsUserLoggedIn(string username)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Username == username)
                return true;
        }
        return false;
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.AcceptIfKey("game_app");
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
