using System.Net;
using System.Text.Json;
using A2S.Application.Push;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using WebPushMessage = Lib.Net.Http.WebPush.PushMessage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace A2S.Infrastructure.Services;

/// <summary>
/// <see cref="IPushSender"/> on <c>Lib.Net.Http.WebPush</c>: VAPID-signed, payload encrypted for the
/// browser's keys. The payload is the JSON the service worker's <c>push</c> handler reads:
/// <c>{ title, body, url, tag }</c>.
/// </summary>
public sealed class WebPushSender : IPushSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PushServiceClient _client;
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(HttpClient httpClient, IOptions<PushOptions> options, ILogger<WebPushSender> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var push = options.Value;
        if (!push.IsConfigured)
        {
            throw new InvalidOperationException("Push:VapidPublicKey, Push:VapidPrivateKey and Push:Subject must all be configured to send web push.");
        }

        _client = new PushServiceClient(httpClient)
        {
            DefaultAuthentication = new VapidAuthentication(push.VapidPublicKey!, push.VapidPrivateKey!)
            {
                Subject = push.Subject!
            }
        };
    }

    public async Task<PushSendStatus> SendAsync(PushTarget target, Application.Push.PushMessage message, CancellationToken ct = default)
    {
        var subscription = new PushSubscription { Endpoint = target.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, target.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, target.Auth);

        var payload = JsonSerializer.Serialize(new { title = message.Title, body = message.Body, url = message.Url, tag = message.Tag }, JsonOptions);
        var pushMessage = new WebPushMessage(payload)
        {
            Topic = message.Tag,
            Urgency = PushMessageUrgency.Normal,
            TimeToLive = (int)TimeSpan.FromHours(1).TotalSeconds
        };

        try
        {
            await _client.RequestPushMessageDeliveryAsync(subscription, pushMessage, ct);
            return PushSendStatus.Sent;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushSendStatus.Gone;
        }
        catch (PushServiceClientException ex)
        {
            _logger.LogWarning(ex, "Push service answered {StatusCode}", (int)ex.StatusCode);
            return PushSendStatus.Failed;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Push delivery request failed");
            return PushSendStatus.Failed;
        }
    }
}
