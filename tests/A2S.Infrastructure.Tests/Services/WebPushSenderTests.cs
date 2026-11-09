using System.Net;
using A2S.Application.Push;
using A2S.Infrastructure.Services;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace A2S.Infrastructure.Tests.Services;

/// <summary>
/// The VAPID sender against a stubbed push service. Keys are an ephemeral pair generated here; no real
/// key is ever committed.
/// </summary>
public class WebPushSenderTests
{
    private static readonly EphemeralVapidKeys Keys = EphemeralVapidKeys.Generate();

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public StubHandler(HttpStatusCode status)
        {
            _status = status;
        }

        // Copied out here: the client disposes the request once the call returns.
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string[] ContentEncoding { get; private set; } = Array.Empty<string>();
        public string[] Topics { get; private set; } = Array.Empty<string>();
        public byte[] Body { get; private set; } = Array.Empty<byte>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            ContentEncoding = request.Content?.Headers.ContentEncoding.ToArray() ?? Array.Empty<string>();
            Topics = request.Headers.TryGetValues("Topic", out var topics) ? topics.ToArray() : Array.Empty<string>();
            Body = request.Content is null ? Array.Empty<byte>() : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(_status) { Content = new StringContent(string.Empty) };
        }
    }

    private static (WebPushSender Sender, StubHandler Handler) Build(HttpStatusCode status, PushOptions? options = null)
    {
        var handler = new StubHandler(status);
        var sender = new WebPushSender(
            new HttpClient(handler),
            Options.Create(options ?? new PushOptions { VapidPublicKey = Keys.PublicKey, VapidPrivateKey = Keys.PrivateKey, Subject = EphemeralVapidKeys.Subject }),
            NullLogger<WebPushSender>.Instance);
        return (sender, handler);
    }

    private static PushTarget Target()
    {
        var (p256dh, auth) = EphemeralVapidKeys.GenerateClientKeys();
        return new PushTarget("https://push.example.com/send/abc", p256dh, auth);
    }

    [Fact]
    public async Task Send_SignsWithVapid_EncryptsThePayload_AndReportsSent()
    {
        var (sender, handler) = Build(HttpStatusCode.Created);

        var status = await sender.SendAsync(Target(), new PushMessage("Time to train — Upper A", "Planned for 17:30.", "/today", "SessionStart"));

        status.Should().Be(PushSendStatus.Sent);
        handler.RequestUri!.ToString().Should().Be("https://push.example.com/send/abc");
        handler.AuthorizationScheme.Should().Be("vapid");
        handler.AuthorizationParameter.Should().Contain("t=").And.Contain("k=" + Keys.PublicKey);
        handler.ContentEncoding.Should().Contain("aes128gcm");
        handler.Topics.Should().Contain("SessionStart");
        handler.Body.Should().NotBeEmpty();
        System.Text.Encoding.UTF8.GetString(handler.Body).Should().NotContain("Time to train", "the payload is encrypted, never sent in clear");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task Send_ReportsGone_WhenThePushServiceHasDroppedTheSubscription(HttpStatusCode status)
    {
        var (sender, _) = Build(status);

        var result = await sender.SendAsync(Target(), new PushMessage("t", "b", "/today", "x"));

        result.Should().Be(PushSendStatus.Gone);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Send_ReportsFailed_OnOtherErrors(HttpStatusCode status)
    {
        var (sender, _) = Build(status);

        var result = await sender.SendAsync(Target(), new PushMessage("t", "b", "/today", "x"));

        result.Should().Be(PushSendStatus.Failed);
    }

    [Fact]
    public void Construction_RequiresAllThreeSettings()
    {
        var act = () => Build(HttpStatusCode.Created, new PushOptions { VapidPublicKey = Keys.PublicKey });
        act.Should().Throw<InvalidOperationException>();
    }
}
