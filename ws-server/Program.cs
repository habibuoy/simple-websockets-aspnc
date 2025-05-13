using System.Net.WebSockets;
using System.Text;
using SimpleWebSockets.Server.Helper;

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

app.MapGet("/", static async (HttpContext httpContext) =>
{
    await httpContext.Response.WriteAsJsonAsync(new { Message = "Hello, World! Access /ws to begin Websocket connection" });
});

app.Map("/ws", static async (HttpContext httpContext, ILogger<Program> logger) =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new { Message = "This endpoint only accepts WebSocket requests." });
    }
    
    using var websocket = await httpContext.WebSockets.AcceptWebSocketAsync();
    logger.LogInformation("WebSocket connection established from {ip}.", httpContext.Connection.RemoteIpAddress);

    var sendMessageTask = SendMessageAsync(websocket);
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

static async Task SendMessageAsync(WebSocket websocket)
{
    while (websocket.State == WebSocketState.Open)
    {
        var message = $"Hello darkness, my old friend! Current time is: {DateTime.Now}";
        var bytes = Encoding.UTF8.GetBytes(message);

        await websocket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length),
                WebSocketMessageType.Text, true, CancellationToken.None);
        await Task.Delay(1000);
    }
}