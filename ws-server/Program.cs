using System.ComponentModel.DataAnnotations;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseWebSockets(new WebSocketOptions() { KeepAliveTimeout = TimeSpan.FromSeconds(3) });

app.UseHttpsRedirection();

app.MapGet("/", static async (HttpContext httpContext) =>
{
    await httpContext.Response.WriteAsJsonAsync(new { Message = "Hello, World! Access /ws to begin Websocket connection" });
});

app.Map("/ws", static async (HttpContext httpContext, ILogger<Program> logger) =>
{
    if (httpContext.WebSockets.IsWebSocketRequest)
    {
        using var websocket = await httpContext.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("WebSocket connection established from {ip}.", httpContext.Connection.RemoteIpAddress);

        var buffer = new byte[1024];
        WebSocketReceiveResult receiveResult = null!;

        TimeSpan pingTimeout = TimeSpan.FromSeconds(5);
        DateTime pingTimeoutDt = DateTime.UtcNow.Add(pingTimeout);

        _ = Task.Run(async () =>
        {
            while (websocket.State == WebSocketState.Open)
            {
                var message = $"Hello darkness, my old friend! Current time is: {DateTime.Now}";
                var bytes = Encoding.UTF8.GetBytes(message);

                await websocket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length),
                        WebSocketMessageType.Text, true, CancellationToken.None);
                await Task.Delay(1000);
            }
        });

        while (websocket.State == WebSocketState.Open)
        {
            if (DateTime.UtcNow > pingTimeoutDt)
            {
                logger.LogInformation("Closing websocket connection of {ip} because of inactivity.", httpContext.Connection.RemoteIpAddress);
                await websocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Client is disconnected",
                    CancellationToken.None);
                break;
            }

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
                        pingTimeoutDt = DateTime.UtcNow.Add(pingTimeout);
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
                logger.LogError("Error happened while receiving result: {ex}. LAST STATE: ({state})", ex, websocket.State);
                if (websocket.State == WebSocketState.Aborted)
                {
                    logger.LogInformation("Aborting websocket connection of {ip}.", httpContext.Connection.RemoteIpAddress);
                    websocket.Abort();
                }
                break;
            }
        }

        logger.LogInformation("Ending websocket request of {ip}.", httpContext.Connection.RemoteIpAddress);
    }
    else
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new { Message = "This endpoint only accepts WebSocket requests." });
    }
});

app.Run();