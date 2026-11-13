using System;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AyInbox.Tests
{
    public class WebhookTests
    {
        private static readonly JsonElement Vector = JsonDocument.Parse(StubHandler.Fixture("webhook_signature.json")).RootElement;
        private static string Secret => Vector.GetProperty("secret").GetString()!;
        private static string Body => Vector.GetProperty("body").GetString()!;
        private static string Header => Vector.GetProperty("header").GetString()!;
        private static DateTimeOffset At(long s) => DateTimeOffset.FromUnixTimeSeconds(s);

        [Fact]
        public void ValidVectorPasses()
        {
            Assert.True(WebhookSignature.Verify(Body, Header, Secret, now: At(1700000000)));
            Assert.True(WebhookSignature.Verify(Encoding.UTF8.GetBytes(Body), Header, Secret, now: At(1700000300)));
        }

        [Fact]
        public void TamperedBodyFails() =>
            Assert.False(WebhookSignature.Verify(Body.Replace("reply_received", "reply_receivee"), Header, Secret, now: At(1700000000)));

        [Fact]
        public void WrongSecretFails() =>
            Assert.False(WebhookSignature.Verify(Body, Header, "whsec_wrong", now: At(1700000000)));

        [Fact]
        public void StrippedPrefixSecretFails() =>
            Assert.False(WebhookSignature.Verify(Body, Header, "test_secret", now: At(1700000000)));

        [Fact]
        public void OutsideToleranceFails() =>
            Assert.False(WebhookSignature.Verify(Body, Header, Secret, now: At(1700000301)));

        [Fact]
        public void MalformedHeadersFail()
        {
            Assert.False(WebhookSignature.Verify(Body, null, Secret, now: At(1700000000)));
            Assert.False(WebhookSignature.Verify(Body, "v1=" + Vector.GetProperty("expected_v1").GetString(), Secret, now: At(1700000000)));
            Assert.False(WebhookSignature.Verify(Body, "t=1700000000", Secret, now: At(1700000000)));
            Assert.False(WebhookSignature.Verify(Body, Header, Secret, toleranceSeconds: 0, now: At(1700000000)));
        }

        [Fact]
        public void AnyV1MatchPasses()
        {
            var header = "t=1700000000,v0=ignored,v1=deadbeef,v1=" + Vector.GetProperty("expected_v1").GetString();
            Assert.True(WebhookSignature.Verify(Body, header, Secret, now: At(1700000000)));
        }

        [Fact]
        public void ConstructEventParsesOrThrows()
        {
            var ev = WebhookSignature.ConstructEvent(Encoding.UTF8.GetBytes(Body), Header, Secret, now: At(1700000000));
            Assert.Equal("campaign.reply_received", ev.EventType);
            Assert.Equal("00000000-0000-4000-8000-000000000001", ev.Id);
            Assert.Throws<SignatureVerificationException>(() =>
                WebhookSignature.ConstructEvent(Encoding.UTF8.GetBytes(Body), Header, "whsec_wrong", now: At(1700000000)));
        }
    }
}
