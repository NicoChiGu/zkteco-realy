using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Tests;

public sealed class WebSocketReplayTests
{
    [Fact]
    public async Task ExpiredCursorReceivesGapThenEarliestAvailableEvents()
    {
        using var factory = new RelayApiFactory(
            new FakeComClientFactory(),
            maximumRetainedEvents: 3);
        _ = factory.CreateClient();
        var hub = factory.Services.GetRequiredService<RealtimeEventHub>();
        for (var index = 1; index <= 5; index++)
        {
            hub.Publish(Event($"event-{index}", index));
        }

        using var socket = await Connect(factory, afterSequence: "0");
        Assert.Equal(
            "websocket_connected",
            (await Receive(socket)).GetProperty("eventType").GetString());
        var gap = await Receive(socket);
        Assert.Equal(
            "event_replay_gap",
            gap.GetProperty("eventType").GetString());
        Assert.Equal(
            "3",
            gap.GetProperty("data")
                .GetProperty("earliestAvailableSequence")
                .GetString());

        var sequences = new List<string?>();
        for (var index = 0; index < 3; index++)
        {
            sequences.Add(
                (await Receive(socket))
                    .GetProperty("eventSequence")
                    .GetString());
        }

        Assert.Equal(["3", "4", "5"], sequences);
    }

    [Fact]
    public async Task SlowConsumerCanReadBeyondFormer512EventBuffer()
    {
        using var factory = new RelayApiFactory(
            new FakeComClientFactory(),
            maximumRetainedEvents: 1_000);
        _ = factory.CreateClient();
        using var socket = await Connect(factory, afterSequence: "0");
        _ = await Receive(socket);

        var hub = factory.Services.GetRequiredService<RealtimeEventHub>();
        for (var index = 1; index <= 600; index++)
        {
            hub.Publish(Event($"slow-{index}", index));
        }

        string? lastSequence = null;
        for (var index = 0; index < 600; index++)
        {
            lastSequence =
                (await Receive(socket))
                    .GetProperty("eventSequence")
                    .GetString();
        }

        Assert.Equal("600", lastSequence);
    }

    private static async Task<WebSocket> Connect(
        RelayApiFactory factory,
        string afterSequence)
    {
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest =
            request => request.Headers["X-API-Key"] = RelayApiFactory.ApiKey;
        return await client.ConnectAsync(
            new Uri(
                $"ws://localhost/api/v1/events/ws?afterSequence={afterSequence}"),
            CancellationToken.None);
    }

    private static async Task<JsonElement> Receive(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, timeout.Token);
            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static RealtimeEvent Event(string eventId, int value) =>
        new(
            eventId,
            "front-door",
            "door_state",
            DateTimeOffset.UtcNow,
            new Dictionary<string, object?> { ["value"] = value });
}
