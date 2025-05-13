using System.Net.WebSockets;

namespace SimpleWebSockets.Server.Models;

public class ChatRoom
{
    private const int MaxUserCount = 2;

    private readonly Dictionary<string, WebSocket?> websocketUsers = new();
    
    public required string Id { get; init; }

    public Dictionary<string, WebSocket?>.ValueCollection WebSockets => websocketUsers.Values;

    public bool HasUser(string user)
    {
        return websocketUsers.Keys.Contains(user);
    }

    public bool CanAssignUser()
    {
        return !(websocketUsers.Count >= MaxUserCount);
    }

    public void AssignUser(string user)
    {
        if (!CanAssignUser())
        {
            throw new InvalidOperationException($"Cannot assign user to chatroom {Id}. The room is full");
        }

        websocketUsers.Add(user, null);
    }

    public void AssignWebsocket(string user, WebSocket webSocket)
    {
        if (!websocketUsers.ContainsKey(user))
        {
            throw new KeyNotFoundException($"Cannot assign websocket. User {user} not found");
        }

        websocketUsers[user] = webSocket;
    }

    public void RemoveWebsocket(string user)
    {
        if (!websocketUsers.ContainsKey(user))
        {
            throw new KeyNotFoundException($"Cannot remove websocket. User {user} not found");
        }

        websocketUsers[user] = null;
    }

    public static ChatRoom Create()
    {
        return new ChatRoom()
        {
            Id = Guid.NewGuid().ToString()
        };
    }
}