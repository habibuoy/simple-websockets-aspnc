namespace SimpleWebSockets.Server.Models;

public class ChatResult
{
    public required string Id { get; set; }
}

public static class ChatResultExtensions
{
    public static ChatResult ToChatResult(this ChatRoom chatRoom)
    {
        return new ChatResult()
        {
            Id = chatRoom.Id
        };
    }
}