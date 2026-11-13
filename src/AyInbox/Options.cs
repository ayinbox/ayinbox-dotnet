using System;
using System.Collections.Generic;
using System.Net.Http;

namespace AyInbox
{
    /// <summary>Settings for <see cref="AyInboxClient"/>.</summary>
    public sealed class AyInboxClientOptions
    {
        /// <summary>API key (<c>aybox_...</c>) or OAuth token (<c>ayat_...</c>). Defaults to <c>AYINBOX_API_KEY</c>.</summary>
        public string? ApiKey { get; set; }

        /// <summary>Base URL. Defaults to <c>AYINBOX_BASE_URL</c>, then <c>https://api.ayinbox.com/v1</c>.</summary>
        public string? BaseUrl { get; set; }

        /// <summary>Timeout of each attempt. Default 30 seconds.</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Automatic retries after the first attempt. Default 2; 0 disables.</summary>
        public int MaxRetries { get; set; } = 2;

        /// <summary>An HttpClient to use. The client never disposes one you pass.</summary>
        public HttpClient? HttpClient { get; set; }
    }

    /// <summary>Options for one call.</summary>
    public sealed class RequestOptions
    {
        /// <summary>Sent as <c>Idempotency-Key</c>. Reused on every retry of this call.</summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>Overrides the client timeout for this call.</summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>Extra headers for this call.</summary>
        public IDictionary<string, string>? Headers { get; set; }

        /// <summary>Overrides the client's retry count for this call.</summary>
        public int? MaxRetries { get; set; }
    }

    /// <summary>A raw HTTP response from <see cref="AyInboxClient.RequestRawAsync"/>.</summary>
    public sealed class ApiResponse
    {
        internal ApiResponse(int status, IReadOnlyDictionary<string, string> headers, byte[] body)
        {
            Status = status;
            Headers = headers;
            Body = body;
        }

        /// <summary>HTTP status code.</summary>
        public int Status { get; }
        /// <summary>Response headers (names compared case-insensitively).</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }
        /// <summary>Response body bytes (empty when there was none).</summary>
        public byte[] Body { get; }
    }
}
