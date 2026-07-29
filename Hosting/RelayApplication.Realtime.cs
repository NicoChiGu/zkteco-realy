using System.Net.WebSockets;
using System.Text.Json;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Hosting;

public static partial class RelayApplication
{
    private static void MapRealtimeEndpoints(WebApplication app)
    {
        app.MapGet("/api/v1/events/ws", async (
            HttpContext context,
            RealtimeEventHub eventHub,
            CancellationToken cancellationToken) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                return Results.BadRequest(new
                {
                    code = "websocket_required",
                    message = "This endpoint requires a WebSocket upgrade request."
                });
            }

            var afterSequenceText =
                context.Request.Query["afterSequence"].ToString();
            if (!string.IsNullOrWhiteSpace(afterSequenceText) &&
                (!long.TryParse(afterSequenceText, out var parsedSequence) ||
                 parsedSequence < 0))
            {
                return Results.BadRequest(new
                {
                    code = "invalid_after_sequence",
                    message =
                        "afterSequence must be a non-negative decimal integer."
                });
            }

            var requestedSequence =
                string.IsNullOrWhiteSpace(afterSequenceText)
                    ? 0L
                    : long.Parse(afterSequenceText);
            var deviceFilter = context.Request.Query["deviceId"].ToString();
            var eventTypes = context.Request.Query["eventType"]
                .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var (earliestSequence, _) = eventHub.GetSequenceRange();
            var replayStartsAt = requestedSequence;
            var hasReplayGap =
                earliestSequence.HasValue &&
                requestedSequence < earliestSequence.Value - 1;
            if (hasReplayGap)
            {
                replayStartsAt = earliestSequence!.Value - 1;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            using var subscription = eventHub.Subscribe(replayStartsAt);

            await SendWebSocketJsonAsync(socket, new
            {
                eventId = Guid.NewGuid().ToString("N"),
                deviceId = string.IsNullOrWhiteSpace(deviceFilter) ? null : deviceFilter,
                eventType = "websocket_connected",
                occurredAt = DateTimeOffset.UtcNow,
                data = new
                {
                    subscriptionId = subscription.Id,
                    filteredEventTypes = eventTypes.OrderBy(value => value).ToArray(),
                    afterSequence = requestedSequence.ToString()
                }
            }, cancellationToken);

            if (hasReplayGap)
            {
                await SendWebSocketJsonAsync(socket, new
                {
                    eventId = Guid.NewGuid().ToString("N"),
                    deviceId =
                        string.IsNullOrWhiteSpace(deviceFilter)
                            ? null
                            : deviceFilter,
                    eventType = "event_replay_gap",
                    occurredAt = DateTimeOffset.UtcNow,
                    data = new
                    {
                        requestedAfterSequence =
                            requestedSequence.ToString(),
                        earliestAvailableSequence =
                            earliestSequence!.Value.ToString()
                    }
                }, cancellationToken);
            }

            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var receiveTask = WaitForWebSocketCloseAsync(socket, connectionCts);

            try
            {
                while (!connectionCts.IsCancellationRequested &&
                       socket.State == WebSocketState.Open)
                {
                    var batch = subscription.ReadNextBatch();
                    if (batch.Count == 0)
                    {
                        await subscription.WaitForEventsAsync(
                            connectionCts.Token);
                        continue;
                    }

                    foreach (var realtimeEvent in batch)
                    {
                        if (!string.IsNullOrWhiteSpace(deviceFilter) &&
                            !string.Equals(
                                deviceFilter,
                                realtimeEvent.DeviceId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (eventTypes.Count > 0 &&
                            !eventTypes.Contains(realtimeEvent.EventType))
                        {
                            continue;
                        }

                        if (socket.State != WebSocketState.Open)
                        {
                            break;
                        }

                        await SendWebSocketJsonAsync(
                            socket,
                            realtimeEvent,
                            connectionCts.Token);
                    }
                }
            }
            catch (OperationCanceledException) when (connectionCts.IsCancellationRequested)
            {
                // Client disconnect, request cancellation, or application shutdown.
            }
            finally
            {
                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closing subscription", CancellationToken.None);
                }
            }

            await receiveTask;
            return Results.Empty;
        });
    }

    private static async Task SendWebSocketJsonAsync(WebSocket socket, object value, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    private static async Task WaitForWebSocketCloseAsync(WebSocket socket, CancellationTokenSource connectionCts)
    {
        var buffer = new byte[1024];
        try
        {
            while (socket.State == WebSocketState.Open && !connectionCts.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, connectionCts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (connectionCts.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (WebSocketException)
        {
            // The remote peer disconnected without a close frame.
        }
        finally
        {
            connectionCts.Cancel();
        }
    }
}
