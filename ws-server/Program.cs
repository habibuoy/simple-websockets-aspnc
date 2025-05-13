using System.Net.WebSockets;
using System.Text;
using SimpleWebSockets.Server.Helpers;
using SimpleWebSockets.Server.Models;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7000, listentOptions => listentOptions.UseHttps());
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseWebSockets();

app.UseHttpsRedirection();

Dictionary<string, ChatRoom> chats = new();

app.MapGet("/", static async (HttpContext httpContext) =>
{
    await httpContext.Response.WriteAsJsonAsync(new { Message = "Hello, World! Access /ws to begin Websocket connection" });
});

app.Map("/ws", static async (HttpContext httpContext, ILogger<Program> logger) =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        await httpContext.Response.WriteAsJsonAsync(new { Message = "This endpoint only accepts WebSocket requests." });
    }

    using var websocket = await httpContext.WebSockets.AcceptWebSocketAsync();
    logger.LogInformation("WebSocket connection established from {ip}.", httpContext.Connection.RemoteIpAddress);

    var sendMessageTask = SendEndlessMessageAsync(websocket, $"Hello darkness, my old friend! Current time is: {DateTime.Now}");
    var timeoutChecker = new WebsocketTimeoutChecker()
    {
        TimeoutDuration = TimeSpan.FromSeconds(5),
        CheckInterval = TimeSpan.FromSeconds(1),
        TimedOut = async () =>
        {
            logger.LogInformation("Closing websocket connection of {ip} because of inactivity.",
                httpContext.Connection.RemoteIpAddress);
            await websocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Client is disconnected",
                CancellationToken.None);
        }
    };

    var timeoutCheckerTask = timeoutChecker.StartAsync(websocket);

    await ReceiveMessageAsync(websocket, logger, httpContext,
        timeoutChecker.IncreaseTimeout);

    await sendMessageTask;

    await timeoutCheckerTask;

    logger.LogInformation("Ending websocket request of {ip}", httpContext.Connection.RemoteIpAddress);
});

app.MapPost("/chat", async ([AsParameters] ChatRequest chatRequest, HttpContext httpContext,
    ILogger<Program> logger) =>
{
    if (chatRequest == null
        || string.IsNullOrEmpty(chatRequest.Sender)
        || string.IsNullOrEmpty(chatRequest.Recipient))
    {
        return Results.BadRequest(new { Message = "Please provide a valid room chat request." });
    }

    if (chats.Values.FirstOrDefault(cr => cr.HasUser(chatRequest!.Sender)
        && cr.HasUser(chatRequest.Recipient)) is not ChatRoom chatRoom)
    {
        chatRoom = ChatRoom.Create();
        chatRoom.AssignUser(chatRequest.Sender);
        chatRoom.AssignUser(chatRequest.Recipient);
        chats.Add(chatRoom.Id, chatRoom);
    }

    return Results.Ok(new { Message = "", Result = chatRoom.ToChatResult() });
});

app.Map("/wschat", async ([AsParameters] WsChatRequest chatRequest, HttpContext httpContext,
    ILogger<Program> logger) =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new { Message = "This endpoint only accepts WebSocket requests." });
    }

    if (chatRequest == null
        || string.IsNullOrEmpty(chatRequest.ChatRoomId)
        || string.IsNullOrEmpty(chatRequest.User))
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new { Message = "Please provide a valid room chat request." });
    }

    if (!chats.TryGetValue(chatRequest!.ChatRoomId, out var chatRoom)
        || !chatRoom.HasUser(chatRequest.User))
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new { Message = "Please provide a valid room chat request." });
    }

    using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
    chatRoom!.AssignWebsocket(chatRequest.User, webSocket);

    string currentTime = DateTime.Now.ToString("HH:mm");

    foreach (var ws in chatRoom.WebSockets)
    {
        if (ws != null)
        {
            await SendMessageAsync(ws, $"([{currentTime}] User {chatRequest.User}) has joined the chat");
        }
    }

    var buffer = new byte[1024];

    while (webSocket.State == WebSocketState.Open)
    {
        var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (receiveResult.MessageType == WebSocketMessageType.Close)
        {
            foreach (var ws in chatRoom.WebSockets)
            {
                if (ws != null)
                {
                    await SendMessageAsync(ws, $"([{currentTime}] User {chatRequest.User}) has left the chat");
                }
            }
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Leave", CancellationToken.None);
            chatRoom.RemoveWebsocket(chatRequest.User);
            break;
        }

        var message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
        foreach (var ws in chatRoom.WebSockets)
        {
            if (ws != null)
            {
                await SendMessageAsync(ws, $"([{currentTime}] {chatRequest.User}): {message}");
            }
        }
    }
});

app.Run();

static async Task ReceiveMessageAsync(WebSocket websocket, ILogger logger, HttpContext httpContext,
    Action? onMessageReceived = null)
{
    var buffer = new byte[1024];
    WebSocketReceiveResult receiveResult = null!;

    while (websocket.State == WebSocketState.Open)
    {
        try
        {
            receiveResult = await websocket.ReceiveAsync(buffer, CancellationToken.None);

            if (receiveResult != null)
            {
                if (!receiveResult.CloseStatus.HasValue)
                {
                    if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        var receivedMessage = Encoding.UTF8.GetString(buffer);
                        logger.LogInformation("Is end of message? {isEnd}. Message: {msg}",
                            receiveResult.EndOfMessage, receivedMessage);
                    }
                    onMessageReceived?.Invoke();
                }
                else
                {
                    if (websocket.State == WebSocketState.CloseReceived)
                    {
                        logger.LogInformation("Receiving websocket close request {stat}, {desc} of {ip} at {dt}. Closing Websocket",
                            receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription,
                            httpContext.Connection.RemoteIpAddress, DateTime.Now);
                        await websocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription,
                            CancellationToken.None);
                    }
                    break;
                }
                receiveResult = null!;
            }
        }
        catch (WebSocketException ex)
        {
            switch (ex.WebSocketErrorCode)
            {
                case WebSocketError.ConnectionClosedPrematurely:
                    logger.LogInformation("Client {ip} closed connection prematurely",
                        httpContext.Connection.RemoteIpAddress);
                    break;
                default:
                    logger.LogError(ex, "Error happened while receiving result. LAST STATE: ({state})", websocket.State);
                    break;
            }
            if (websocket.State == WebSocketState.Aborted)
            {
                logger.LogInformation("Aborting websocket connection of {ip}.", httpContext.Connection.RemoteIpAddress);
                websocket.Abort();
            }
            break;
        }
    }
}

static async Task SendMessageAsync(WebSocket websocket, string message)
{
    var bytes = Encoding.UTF8.GetBytes(message);

    await websocket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length),
        WebSocketMessageType.Text, true, CancellationToken.None);
}

static async Task SendEndlessMessageAsync(WebSocket websocket, string message, int interval = 1000)
{
    while (websocket.State == WebSocketState.Open)
    {
        await SendMessageAsync(websocket, message);
        await Task.Delay(interval);
    }
}