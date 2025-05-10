// See https://aka.ms/new-console-template for more information

using System.Net.WebSockets;
using System.Text;

const string WebsocketEndpoint = "wss://localhost:7000/ws";

ClientWebSocket websocket = null!;

async Task RunWebsocket()
{
    if (websocket == null)
    {
        var answer = string.Empty;

        while (answer != "Y" && answer != "y")
        {
            Console.Write("Begin websocket? (Y/y): ");
            answer = Console.ReadLine();
        }

        websocket = new ClientWebSocket();
    }
    else
    {
        await websocket.ConnectAsync(new Uri(WebsocketEndpoint), CancellationToken.None);

        var buffer = new byte[1024];
        var message = Encoding.UTF8.GetBytes("Ping");

        while (true)
        {
            WebSocketReceiveResult receiveResult = null!;

            await websocket.SendAsync(message, WebSocketMessageType.Text, true, CancellationToken.None);
            receiveResult = await websocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (receiveResult != null)
            {
                if (!receiveResult.CloseStatus.HasValue)
                {
                    if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        var receivedMessage = Encoding.UTF8.GetString(buffer);
                        Console.WriteLine($"Message from server: {receivedMessage}");
                    }
                }
                else
                {
                    await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal", CancellationToken.None);
                    break;
                }
            }

            await Task.Delay(1000);
        }
        
        websocket = null!;
    }
}

while (true)
{
    await RunWebsocket();
}