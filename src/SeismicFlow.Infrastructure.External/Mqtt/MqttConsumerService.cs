using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Application.Mqtt;
using SeismicFlow.Domain.Aggregates.Readings;

namespace SeismicFlow.Infrastructure.External.Mqtt;

/// <summary>
/// Long-running background service that:
///   1. Connects to Mosquitto as an internal subscriber
///   2. Subscribes to  tenant/+/devices/+/data
///   3. Parses each message → creates SeismicReading → persists via ISeismicReadingPersister
///   4. Publishes in-process ReadingReceivedEvent for SSE streaming
/// </summary>
public sealed class MqttConsumerService(
    IServiceScopeFactory scopeFactory,
    IReadingEventBus eventBus,
    IOptions<MqttConsumerOptions> opts,
    ILogger<MqttConsumerService> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string TopicFilter = "tenant/+/devices/+/data";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mqttFactory = new MqttClientFactory();
        using var client = mqttFactory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += OnMessageAsync;

        var connectOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(opts.Value.Host, opts.Value.Port)
            .WithClientId($"seismicflow-consumer-{Guid.NewGuid():N}")
            .WithCredentials(opts.Value.Username, opts.Value.Password)
            .WithCleanSession(false)
            .Build();

        // Retry loop — broker may not be ready at startup
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await client.ConnectAsync(connectOptions, stoppingToken);
                logger.LogInformation("MQTT consumer connected to {Host}:{Port}",
                    opts.Value.Host, opts.Value.Port);

                var subOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(f => f.WithTopic(TopicFilter))
                    .Build();

                await client.SubscribeAsync(subOptions, stoppingToken);
                logger.LogInformation("Subscribed to {Topic}", TopicFilter);

                // Block until disconnected or cancelled
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MQTT connection failed — retrying in 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            finally
            {
                if (client.IsConnected)
                    await client.DisconnectAsync(cancellationToken: CancellationToken.None);
            }
        }
    }

    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;

        if (!TryParseTopic(topic, out var tenantId, out var deviceId))
        {
            logger.LogWarning("Unexpected topic format: {Topic}", topic);
            return;
        }

        MqttReadingPayload? payload;
        try
        {
            var json = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
            payload = JsonSerializer.Deserialize<MqttReadingPayload>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialise MQTT payload on topic {Topic}", topic);
            return;
        }

        if (payload is null || payload.Samples.Length == 0)
            return;

        var reading = SeismicReading.Create(
            deviceId,
            payload.Channel,
            DateTimeOffset.FromUnixTimeMilliseconds(payload.TimestampMs),
            payload.SampleRate,
            payload.Samples);

        try
        {
            // ISeismicReadingPersister — no direct Persistence reference needed
            using var scope = scopeFactory.CreateScope();
            var persister = scope.ServiceProvider.GetRequiredService<ISeismicReadingPersister>();
            await persister.PersistAsync(tenantId, reading);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist reading for device {DeviceId}", deviceId);
            return;
        }

        eventBus.Publish(new ReadingReceivedEvent(
            tenantId,
            deviceId,
            reading.Channel,
            reading.Timestamp,
            reading.SampleRate,
            reading.Samples));

        logger.LogDebug("Persisted reading {ReadingId} device={DeviceId} ch={Channel}",
            reading.Id, deviceId, reading.Channel);
    }

    private static bool TryParseTopic(string topic, out Guid tenantId, out Guid deviceId)
    {
        tenantId = default;
        deviceId = default;

        var parts = topic.Split('/');
        if (parts.Length != 5) return false;
        if (parts[0] != "tenant" || parts[2] != "devices" || parts[4] != "data") return false;

        return Guid.TryParse(parts[1], out tenantId) &&
               Guid.TryParse(parts[3], out deviceId);
    }
}

public sealed class MqttConsumerOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string Username { get; set; } = "sf-consumer";
    public string Password { get; set; } = "sf-consumer-secret";
}