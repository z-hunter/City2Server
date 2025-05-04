using LiteNetLib;

public class PlayerSession {
    public string Username { get; set; }
    public string Token { get; set; }
    public NetPeer Peer { get; set; }

    public PlayerSession(string username, string token, NetPeer peer) {
        Username = username;
        Token = token;
        Peer = peer;
    }
}
