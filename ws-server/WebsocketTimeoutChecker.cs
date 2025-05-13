using System.Net.WebSockets;

namespace SimpleWebSockets.Server.Helper;

public class WebsocketTimeoutChecker
{
    public TimeSpan CheckInterval { get; init; }
    public TimeSpan TimeoutDuration { get; init; }
    public Action? TimedOut { get; init; }

    protected DateTime NextTimeout { get; set; }
    protected TaskCompletionSource<bool> tcs = new();

    public Task<bool> StartAsync(WebSocket websocket)
    {
        IncreaseTimeout();
        _ = RunChecking(websocket);

        return tcs.Task;
    }

    private async Task RunChecking(WebSocket websocket)
    {
        while (websocket.State == WebSocketState.Open)
        {
            if (DateTime.Now >= NextTimeout)
            {
                TimedOut?.Invoke();
                tcs.TrySetResult(true);
                break;
            }

            await Task.Delay(CheckInterval);
        }

        tcs.TrySetResult(false);
    }

    public void IncreaseTimeout()
    {
        NextTimeout = DateTime.Now.Add(TimeoutDuration);
    }
}