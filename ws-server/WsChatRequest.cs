namespace SimpleWebSockets.Server.Models;

public class WsChatRequest
{
    public required string ChatRoomId { get; set; }
    public required string User { get; set; }
}