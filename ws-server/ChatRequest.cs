namespace SimpleWebSockets.Server.Models;

public class ChatRequest
{
    public required string Sender { get; set; }
    public required string Recipient { get; set; }
}