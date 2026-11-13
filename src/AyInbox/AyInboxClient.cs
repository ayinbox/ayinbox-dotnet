using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AyInbox
{
    /// <summary>Client for the AY Inbox REST API.</summary>
    public sealed class AyInboxClient : IDisposable
    {
        /// <summary>This library's version.</summary>
        public const string Version = "0.1.0";

        /// <summary>The default base URL.</summary>
        public const string DefaultBaseUrl = "https://api.ayinbox.com/v1";

        private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(120);
        private static readonly Random Jitter = new Random();

        internal static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly string _apiKey;
        private readonly HttpClient _http;
        private readonly bool _ownsHttp;
        private readonly TimeSpan _timeout;
        private readonly int _maxRetries;

        internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (d, ct) => Task.Delay(d, ct);

        /// <summary>Creates a client from <c>AYINBOX_API_KEY</c> and <c>AYINBOX_BASE_URL</c>.</summary>
        public AyInboxClient() : this(new AyInboxClientOptions()) { }

        /// <summary>Creates a client with an explicit API key.</summary>
        public AyInboxClient(string apiKey) : this(new AyInboxClientOptions { ApiKey = apiKey }) { }

        /// <summary>Creates a client from options.</summary>
        public AyInboxClient(AyInboxClientOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var key = options.ApiKey;
            if (string.IsNullOrEmpty(key)) key = Environment.GetEnvironmentVariable("AYINBOX_API_KEY");
            if (string.IsNullOrEmpty(key))
                throw new AyInboxException("No API key: pass one or set AYINBOX_API_KEY.");
            _apiKey = key!;

            var baseUrl = options.BaseUrl;
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = Environment.GetEnvironmentVariable("AYINBOX_BASE_URL");
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = DefaultBaseUrl;
            BaseUrl = baseUrl!.TrimEnd('/');

            _timeout = options.Timeout;
            _maxRetries = Math.Max(0, options.MaxRetries);
            if (options.HttpClient != null)
            {
                _http = options.HttpClient;
                _ownsHttp = false;
            }
            else
            {
                _http = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
                _ownsHttp = true;
            }

            Me = new MeResource(this);
            Campaigns = new CampaignsResource(this);
            Contacts = new ContactsResource(this);
            Mailboxes = new MailboxesResource(this);
            Unibox = new UniboxResource(this);
        }

        /// <summary>The base URL in use, without a trailing slash.</summary>
        public string BaseUrl { get; }

        /// <summary><c>GET /me</c>.</summary>
        public MeResource Me { get; }
        /// <summary>Campaigns.</summary>
        public CampaignsResource Campaigns { get; }
        /// <summary>Contacts.</summary>
        public ContactsResource Contacts { get; }
        /// <summary>Mailboxes (<c>/emails</c>).</summary>
        public MailboxesResource Mailboxes { get; }
        /// <summary>Unibox threads.</summary>
        public UniboxResource Unibox { get; }

        /// <inheritdoc />
        public override string ToString() => $"AyInboxClient(BaseUrl={BaseUrl}, ApiKey={Redact(_apiKey)})";

        private static string Redact(string key) => key.Length > 10 ? key.Substring(0, 6) + "..." : "***";

        /// <summary>Disposes the HttpClient only when this client created it.</summary>
        public void Dispose()
        {
            if (_ownsHttp) _http.Dispose();
        }

        /// <summary>
        /// Calls any route and returns the decoded JSON, or null for an empty body.
        /// A body that is not JSON comes back as a JSON string holding its text.
        /// </summary>
        /// <param name="method">HTTP method, for example <c>GET</c>.</param>
        /// <param name="path">Path relative to the base URL, for example <c>/unibox/count</c>.</param>
        /// <param name="query">Query parameters; null values are skipped.</param>
        /// <param name="body">Any value System.Text.Json can serialize, sent as JSON.</param>
        /// <param name="options">Per-call options.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        public async Task<JsonElement?> RequestAsync(string method, string path,
            IEnumerable<KeyValuePair<string, string?>>? query = null, object? body = null,
            RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var res = await SendAsync(method, path, query, body, options, false, cancellationToken).ConfigureAwait(false);
            return Decode(res.Body);
        }

        /// <summary>Calls any route and returns status, headers and raw bytes.</summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Path relative to the base URL.</param>
        /// <param name="query">Query parameters; null values are skipped.</param>
        /// <param name="body">Any value System.Text.Json can serialize, sent as JSON.</param>
        /// <param name="options">Per-call options.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        public Task<ApiResponse> RequestRawAsync(string method, string path,
            IEnumerable<KeyValuePair<string, string?>>? query = null, object? body = null,
            RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            SendAsync(method, path, query, body, options, false, cancellationToken);

        /// <summary>
        /// Walks every page of a paginated route (one using <c>data</c> and <c>pagination</c>) and yields each row.
        /// For a POST search the same body is sent on every page.
        /// </summary>
        /// <param name="path">Path relative to the base URL.</param>
        /// <param name="query">Query parameters other than <c>cursor</c>.</param>
        /// <param name="method">HTTP method, <c>GET</c> by default.</param>
        /// <param name="body">Body sent on every page.</param>
        /// <param name="options">Per-call options.</param>
        /// <param name="cancellationToken">Cancels the walk.</param>
        public IAsyncEnumerable<JsonElement> PaginateAsync(string path,
            IEnumerable<KeyValuePair<string, string?>>? query = null, string method = "GET", object? body = null,
            RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            WalkAsync(cursor => GetPageAsync<JsonElement>(method, path, WithCursor(query, cursor), body, options, true, cancellationToken), cancellationToken);

        internal static List<KeyValuePair<string, string?>> WithCursor(IEnumerable<KeyValuePair<string, string?>>? query, string? cursor)
        {
            var q = query?.Where(kv => kv.Key != "cursor").ToList() ?? new List<KeyValuePair<string, string?>>();
            if (!string.IsNullOrEmpty(cursor)) q.Add(new KeyValuePair<string, string?>("cursor", cursor));
            return q;
        }

        internal static async IAsyncEnumerable<T> WalkAsync<T>(Func<string?, Task<Page<T>>> fetch,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string? cursor = null;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await fetch(cursor).ConfigureAwait(false);
                foreach (var item in page.Data) yield return item;
                if (!page.HasMore || string.IsNullOrEmpty(page.NextCursor)) yield break;
                cursor = page.NextCursor;
            }
        }

        internal async Task<T> GetAsync<T>(string method, string path, IEnumerable<KeyValuePair<string, string?>>? query,
            object? body, RequestOptions? options, bool safeRead, CancellationToken ct)
        {
            var res = await SendAsync(method, path, query, body, options, safeRead, ct).ConfigureAwait(false);
            if (res.Body.Length == 0) return default!;
            try
            {
                return JsonSerializer.Deserialize<T>(res.Body, Json)!;
            }
            catch (JsonException e)
            {
                throw new AyInboxException($"Could not decode the response of {method} {path}: {e.Message}", e);
            }
        }

        internal async Task<Page<T>> GetPageAsync<T>(string method, string path, IEnumerable<KeyValuePair<string, string?>>? query,
            object? body, RequestOptions? options, bool safeRead, CancellationToken ct)
        {
            var raw = await GetAsync<JsonElement>(method, path, query, body, options, safeRead, ct).ConfigureAwait(false);
            return Page<T>.FromJson(raw);
        }

        internal async Task<ApiResponse> SendAsync(string method, string path, IEnumerable<KeyValuePair<string, string?>>? query,
            object? body, RequestOptions? options, bool safeRead, CancellationToken ct)
        {
            var url = BuildUrl(path, query);
            var upper = method.ToUpperInvariant();
            var idemKey = options?.IdempotencyKey;
            var maxRetries = Math.Max(0, options?.MaxRetries ?? _maxRetries);
            var timeout = options?.Timeout ?? _timeout;
            var unsafeWithoutKey = (upper == "POST" || upper == "PATCH") && string.IsNullOrEmpty(idemKey) && !safeRead;
            byte[]? payload = body == null ? null : JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), Json);

            for (var attempt = 0; ; attempt++)
            {
                using var req = new HttpRequestMessage(new HttpMethod(upper), url);
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);
                req.Headers.TryAddWithoutValidation("User-Agent", "ayinbox-dotnet/" + Version);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrEmpty(idemKey)) req.Headers.TryAddWithoutValidation("Idempotency-Key", idemKey);
                if (options?.Headers != null)
                    foreach (var h in options.Headers)
                    {
                        if (string.Equals(h.Key, "Authorization", StringComparison.OrdinalIgnoreCase)) continue;
                        req.Headers.Remove(h.Key);
                        req.Headers.TryAddWithoutValidation(h.Key, h.Value);
                    }
                if (payload != null)
                {
                    req.Content = new ByteArrayContent(payload);
                    req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                if (timeout > TimeSpan.Zero && timeout != System.Threading.Timeout.InfiniteTimeSpan) cts.CancelAfter(timeout);

                AyInboxException failure;
                TimeSpan? wait = null;
                bool retryable;
                try
                {
                    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
                    var bytes = resp.Content == null ? Array.Empty<byte>() : await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    var headers = CollectHeaders(resp);
                    var status = (int)resp.StatusCode;
                    if (status >= 200 && status < 300) return new ApiResponse(status, headers, bytes);

                    var err = ApiException.FromResponse(status, Encoding.UTF8.GetString(bytes), headers);
                    failure = err;
                    if (err.RetryAfterSeconds.HasValue) wait = TimeSpan.FromSeconds(err.RetryAfterSeconds.Value);
                    retryable = status == 429 || ((status == 502 || status == 503 || status == 504) && !unsafeWithoutKey);
                }
                catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
                {
                    failure = new AyInboxTimeoutException($"{upper} {path} timed out after {timeout.TotalSeconds:0.###}s.", e);
                    retryable = !unsafeWithoutKey;
                }
                catch (HttpRequestException e)
                {
                    failure = new ConnectionException($"{upper} {path} failed: {e.Message}", e);
                    retryable = !unsafeWithoutKey;
                }

                if (!retryable || attempt >= maxRetries) throw failure;
                await Delay(BackoffFor(attempt, wait), ct).ConfigureAwait(false);
            }
        }

        internal static TimeSpan BackoffFor(int attempt, TimeSpan? retryAfter)
        {
            if (retryAfter.HasValue)
                return retryAfter.Value < TimeSpan.Zero ? TimeSpan.Zero : (retryAfter.Value > MaxRetryAfter ? MaxRetryAfter : retryAfter.Value);
            var ceiling = Math.Min(8.0, 0.5 * Math.Pow(2, attempt));
            double r;
            lock (Jitter) r = Jitter.NextDouble();
            return TimeSpan.FromSeconds(r * ceiling);
        }

        internal static TimeSpan? ParseRetryAfter(IReadOnlyDictionary<string, string> headers)
        {
            if (!headers.TryGetValue("Retry-After", out var v) || string.IsNullOrWhiteSpace(v)) return null;
            if (double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var secs))
                return TimeSpan.FromSeconds(Math.Max(0, secs));
            if (DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
            {
                var d = at - DateTimeOffset.UtcNow;
                return d < TimeSpan.Zero ? TimeSpan.Zero : d;
            }
            return null;
        }

        private static IReadOnlyDictionary<string, string> CollectHeaders(HttpResponseMessage resp)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in resp.Headers) d[h.Key] = string.Join(", ", h.Value);
            if (resp.Content != null)
                foreach (var h in resp.Content.Headers) d[h.Key] = string.Join(", ", h.Value);
            return d;
        }

        private string BuildUrl(string path, IEnumerable<KeyValuePair<string, string?>>? query)
        {
            var sb = new StringBuilder(BaseUrl);
            if (!path.StartsWith("/", StringComparison.Ordinal)) sb.Append('/');
            sb.Append(path);
            var sep = path.IndexOf('?') >= 0 ? '&' : '?';
            if (query != null)
                foreach (var kv in query)
                {
                    if (kv.Value == null) continue;
                    sb.Append(sep).Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value));
                    sep = '&';
                }
            return sb.ToString();
        }

        private static JsonElement? Decode(byte[] body)
        {
            if (body.Length == 0) return null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                using var doc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(Encoding.UTF8.GetString(body)));
                return doc.RootElement.Clone();
            }
        }

        internal static string Seg(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("An id is required.", nameof(id));
            return Uri.EscapeDataString(id);
        }
    }
}
