using System.Text;
using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Web.Infrastructure;

namespace Backend.Web.Endpoints;

public class Sse : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/sse";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapGet(Stream, "");
    }

    [EndpointSummary("SSE Stream")]
    [EndpointDescription("Server-Sent Events stream. Accepts JWT via Authorization header OR ?access_token= query param (for browser EventSource). Supports Last-Event-ID for reconnect recovery.")]
    public static async Task Stream(
        ISseConnectionManager sse,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
        {
            ctx.Response.StatusCode = 401;
            return;
        }

        ctx.Response.Headers.Append("Content-Type", "text/event-stream");
        ctx.Response.Headers.Append("Cache-Control", "no-cache");
        ctx.Response.Headers.Append("Connection", "keep-alive");
        ctx.Response.Headers.Append("X-Accel-Buffering", "no");
        // Tell browser/proxy to retry after 3 seconds on disconnect
        ctx.Response.Headers.Append("Access-Control-Allow-Origin", "*");

        var lastEventId = ctx.Request.Headers["Last-Event-ID"].FirstOrDefault();
        var eventCounter = long.TryParse(lastEventId, out var lastId) ? lastId : 0L;

        // Heartbeat task: keeps alive through proxies and NAT timeouts
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(25));
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await heartbeatTimer.WaitForNextTickAsync(ct);
                    await WriteRawAsync(ctx.Response, ": ping\n\n", ct);
                }
                catch (OperationCanceledException) { break; }
                catch { break; }
            }
        }, ct);

        // Connected event — client knows it's live
        eventCounter++;
        await WriteEventAsync(ctx.Response, "connected",
            JsonSerializer.Serialize(new { userId, ts = DateTimeOffset.UtcNow }),
            eventCounter.ToString(), ct);

        await foreach (var evt in sse.SubscribeAsync(userId, ct))
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                eventCounter++;
                await WriteEventAsync(ctx.Response, evt.EventType, evt.Payload,
                    evt.Id ?? eventCounter.ToString(), ct);
            }
            catch (OperationCanceledException) { break; }
            catch { break; }
        }
    }

    private static async Task WriteEventAsync(
        HttpResponse response,
        string eventType,
        string data,
        string? id,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        if (id is not null) sb.AppendLine($"id: {id}");
        sb.AppendLine($"event: {eventType}");
        // SSE spec: each line of data gets its own "data:" prefix
        foreach (var line in data.Split('\n'))
            sb.AppendLine($"data: {line}");
        // Retry hint: tell client to reconnect after 3s if stream drops
        sb.AppendLine("retry: 3000");
        sb.AppendLine();

        await WriteRawAsync(response, sb.ToString(), ct);
    }

    private static async Task WriteRawAsync(HttpResponse response, string text, CancellationToken ct)
    {
        await response.WriteAsync(text, ct);
        await response.Body.FlushAsync(ct);
    }
}
