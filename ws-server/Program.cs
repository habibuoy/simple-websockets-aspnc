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

app.UseWebSockets(new WebSocketOptions());

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

        while (true)
        {
            if (DateTime.UtcNow > pingTimeoutDt)
            {
                logger.LogInformation("Closing websocket connection of {ip} because of inactivity.", httpContext.Connection.RemoteIpAddress);
                await websocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Client is disconnected",
                    CancellationToken.None);
                break;
            }

            var message = $"Hello darkness, my old friend! Current time is: {DateTime.Now}";
            var bytes = Encoding.UTF8.GetBytes(message);

            await Task.WhenAny(
                Task.Run(async () =>
                {
                    receiveResult = await websocket.ReceiveAsync(buffer, CancellationToken.None);
                }),
                Task.Run(async () =>
                {
                    await websocket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length),
                        WebSocketMessageType.Text, true, CancellationToken.None);
                    await Task.Delay(1000);
                })
            );

            if (receiveResult != null)
            {
                if (!receiveResult.CloseStatus.HasValue)
                {
                    logger.LogInformation("Receiving ping of websocket connection of {ip}.", httpContext.Connection.RemoteIpAddress);
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
                    logger.LogInformation("Receiving websocket close request of {ip}. Closing Websocket", httpContext.Connection.RemoteIpAddress);
                    await websocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription,
                        CancellationToken.None);
                    break;
                }
                receiveResult = null!;
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