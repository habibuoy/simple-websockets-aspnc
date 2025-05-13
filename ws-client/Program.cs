// See https://aka.ms/new-console-template for more information

using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

const string WebsocketEndpoint = "wss://localhost:7000/ws";
const string ChatEndpoint = "wss://localhost:7000/chat?";
const string WebsocketChatEndpoint = "wss://localhost:7000/wschat?";

string[] validAnswers = ["1", "2"];

ClientWebSocket? websocket = null;
string? answer = string.Empty;

async Task RunWebsocket()
{
    if (websocket == null && string.IsNullOrEmpty(answer))
    {
        while (!validAnswers.Contains(answer))
        {
            Console.WriteLine("Choose feature:\n1. Websocket Test\n2. Websocket Chat");
            Console.Write("Your choice? (1/2): ");
            answer = Console.ReadLine();
        }
    }
    else
    {
        if (answer == "1")
        {
            websocket = new ClientWebSocket();

            await websocket.ConnectAsync(new Uri(WebsocketEndpoint), CancellationToken.None);

            var buffer = new byte[1024];
            var message = Encoding.UTF8.GetBytes("Ping");

            _ = Task.Run(async () =>
            {
                while (websocket.State == WebSocketState.Open)
                {
                    await websocket.SendAsync(message, WebSocketMessageType.Text, true, CancellationToken.None);
                    await Task.Delay(500);
                }
            });

            while (websocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult receiveResult = null!;

                try
                {
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
                            if (websocket.State == WebSocketState.CloseReceived)
                            {
                                Console.WriteLine("Server is closing connection at {0}. Stat {1}, desc {2}.",
                                    DateTime.Now, receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription);
                                await websocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription,
                                        CancellationToken.None);
                                websocket = null;
                            }
                            break;
                        }
                    }
                }
                catch (WebSocketException)
                {
                    Console.WriteLine($"Error happened while receiving message. LAST STATE: ({websocket.State})");
                    break;
                }
            }
        }
        else if (answer == "2")
        {
            Console.Write("Enter user: ");
            string? user = Console.ReadLine();

            while (string.IsNullOrEmpty(user))
            {
                Console.WriteLine("User cannot be empty");
                Console.Write("Enter user: ");
                user = Console.ReadLine();
            }

            Console.Write("Enter recipient: ");
            string? recipient = Console.ReadLine();

            while (string.IsNullOrEmpty(recipient))
            {
                Console.WriteLine("recipient cannot be empty");
                Console.Write("Enter recipient: ");
                recipient = Console.ReadLine();
            }

            Console.WriteLine("Getting chat...");

            var httpClient = new HttpClient();
            string? chatId = null;

            try
            {
                var chatResult = await httpClient.PostAsync(ChatEndpoint + $"sender={user}&recipient={recipient}", null);
                var resultContent = chatResult.Content;

                if (!chatResult.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to get chat, code {chatResult.StatusCode}: {resultContent}");
                    return;
                }

                var jsonNode = await JsonNode.ParseAsync(resultContent.ReadAsStream());
                if (jsonNode == null)
                {
                    Console.WriteLine($"Failed to parse chat result");
                    return;
                }

                var result = jsonNode["result"];
                if (result == null)
                {
                    Console.WriteLine($"Failed to parse chat result");
                    return;
                }

                var chatIdNode = result["id"];
                if (chatIdNode == null)
                {
                    Console.WriteLine($"Failed to parse chat result");
                    return;
                }

                chatId = chatIdNode!.GetValue<string>();

                Console.WriteLine($"Success getting chat. Chat id: {chatId}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine("Error happened while accessing chat endpoint:");
                Console.WriteLine(ex);
                websocket = null;
                return;
            }
            finally
            {
                httpClient.Dispose();
                Console.WriteLine("");
            }

            websocket = new ClientWebSocket();

            Console.WriteLine("Connecting to chat");
            try
            {
                await websocket!.ConnectAsync(new Uri(WebsocketChatEndpoint + $"chatRoomId={chatId}&user={user}"),
                    CancellationToken.None);

                Console.WriteLine("Connected to chat");
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"Error while connecting to chat: {ex.WebSocketErrorCode}");
                Console.WriteLine(ex);
                return;
            }
            finally
            {
            }

            var receivingTask = Task.Run(async () =>
            {
                var buffer = new byte[1024];
                while (websocket!.State == WebSocketState.Open)
                {
                    try
                    {
                        var receiveResult = await websocket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, buffer.Length),
                            CancellationToken.None);
                        if (receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            Console.WriteLine("Closing websocket");
                            await websocket.CloseAsync(receiveResult.CloseStatus!.Value,
                                receiveResult.CloseStatusDescription, CancellationToken.None);
                            break;
                        }

                        if (receiveResult.MessageType == WebSocketMessageType.Text)
                        {
                            var message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                            Console.WriteLine(message);
                        }
                    }
                    catch (WebSocketException ex)
                    {
                        Console.WriteLine($"Error while receiving websocket message: {ex.WebSocketErrorCode}");
                        Console.WriteLine(ex);
                        continue;
                    }
                }
            });

            await Task.Delay(1000);

            var sendingTask = Task.Run(async () =>
            {
                while (websocket!.State == WebSocketState.Open)
                {
                    try
                    {
                        Console.Write("Write message: ");
                        string message = Console.ReadLine() ?? string.Empty;

                        if (message == "endchat")
                        {
                            await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Ending chat",
                                CancellationToken.None);
                            websocket = null;
                            break;
                        }

                        await websocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
                            WebSocketMessageType.Text, true, CancellationToken.None);
                        await Task.Delay(500);
                    }
                    catch (WebSocketException ex)
                    {
                        Console.WriteLine($"Error while sending websocket message: {ex.WebSocketErrorCode}");
                        Console.WriteLine(ex);
                        continue;
                    }
                }
            });

            await receivingTask;
            await sendingTask;
        }

        answer = string.Empty;
    }
}

while (true)
{
    await RunWebsocket();
}