using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AyInbox
{
    /// <summary>A webhook delivery body.</summary>
    public sealed class WebhookEvent : ApiObject
    {
        /// <summary>Event id; equals the <c>X-Ayinbox-Event-Id</c> header. Use it to drop retried deliveries.</summary>
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        /// <summary>Event type, for example <c>campaign.reply_received</c>.</summary>
        [JsonPropertyName("event_type")] public string EventType { get; set; } = "";
        /// <summary>Workspace id.</summary>
        [JsonPropertyName("organization_id")] public string? OrganizationId { get; set; }
        /// <summary>When the event happened.</summary>
        [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
        /// <summary>Event payload; its shape depends on the event type.</summary>
        [JsonPropertyName("data")] public JsonElement Data { get; set; }
    }

    /// <summary>Verifies the <c>X-Ayinbox-Signature</c> header of a webhook delivery.</summary>
    public static class WebhookSignature
    {
        /// <summary>The default timestamp tolerance, in seconds.</summary>
        public const int DefaultToleranceSeconds = 300;

        /// <summary>Returns true when the header signs this raw body with this secret and the timestamp is within tolerance.</summary>
        /// <param name="payload">The raw request body, before any JSON parsing.</param>
        /// <param name="header">The <c>X-Ayinbox-Signature</c> header value.</param>
        /// <param name="secret">The endpoint secret, <c>whsec_</c> prefix included.</param>
        /// <param name="toleranceSeconds">Largest allowed clock difference.</param>
        /// <param name="now">The current time; defaults to the system clock.</param>
        public static bool Verify(byte[] payload, string? header, string secret, int toleranceSeconds = DefaultToleranceSeconds, DateTimeOffset? now = null)
        {
            if (payload == null || string.IsNullOrEmpty(header) || string.IsNullOrEmpty(secret) || toleranceSeconds <= 0) return false;

            long? t = null;
            var sigs = new System.Collections.Generic.List<string>();
            foreach (var part in header!.Split(','))
            {
                var i = part.IndexOf('=');
                if (i <= 0) continue;
                var k = part.Substring(0, i).Trim();
                var v = part.Substring(i + 1).Trim();
                if (k == "t" && long.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var ts)) t = ts;
                else if (k == "v1" && v.Length > 0) sigs.Add(v);
            }
            if (t == null || sigs.Count == 0) return false;

            var current = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
            if (Math.Abs(current - t.Value) > toleranceSeconds) return false;

            var prefix = Encoding.UTF8.GetBytes(t.Value.ToString(CultureInfo.InvariantCulture) + ".");
            var message = new byte[prefix.Length + payload.Length];
            Buffer.BlockCopy(prefix, 0, message, 0, prefix.Length);
            Buffer.BlockCopy(payload, 0, message, prefix.Length, payload.Length);
            byte[] digest;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret))) digest = hmac.ComputeHash(message);
            var expected = Encoding.ASCII.GetBytes(ToHex(digest));

            var ok = false;
            foreach (var s in sigs)
                if (FixedTimeEquals(expected, Encoding.ASCII.GetBytes(s))) ok = true;
            return ok;
        }

        /// <summary>Verifies a body given as a string (its UTF-8 bytes are what was signed).</summary>
        /// <param name="payload">The raw request body.</param>
        /// <param name="header">The <c>X-Ayinbox-Signature</c> header value.</param>
        /// <param name="secret">The endpoint secret, <c>whsec_</c> prefix included.</param>
        /// <param name="toleranceSeconds">Largest allowed clock difference.</param>
        /// <param name="now">The current time; defaults to the system clock.</param>
        public static bool Verify(string payload, string? header, string secret, int toleranceSeconds = DefaultToleranceSeconds, DateTimeOffset? now = null) =>
            Verify(Encoding.UTF8.GetBytes(payload ?? ""), header, secret, toleranceSeconds, now);

        /// <summary>Verifies the signature, then parses the body. Throws <see cref="SignatureVerificationException"/> on failure.</summary>
        /// <param name="payload">The raw request body.</param>
        /// <param name="header">The <c>X-Ayinbox-Signature</c> header value.</param>
        /// <param name="secret">The endpoint secret, <c>whsec_</c> prefix included.</param>
        /// <param name="toleranceSeconds">Largest allowed clock difference.</param>
        /// <param name="now">The current time; defaults to the system clock.</param>
        public static WebhookEvent ConstructEvent(byte[] payload, string? header, string secret, int toleranceSeconds = DefaultToleranceSeconds, DateTimeOffset? now = null)
        {
            if (!Verify(payload, header, secret, toleranceSeconds, now))
                throw new SignatureVerificationException("The webhook signature did not verify.");
            try
            {
                return JsonSerializer.Deserialize<WebhookEvent>(payload, AyInboxClient.Json)
                       ?? throw new SignatureVerificationException("The webhook body is empty.");
            }
            catch (JsonException e)
            {
                throw new SignatureVerificationException("The webhook body is not valid JSON: " + e.Message);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
#if NETSTANDARD2_0
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
#else
            return CryptographicOperations.FixedTimeEquals(a, b);
#endif
        }
    }
}
