using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AyInbox.Tests
{
    internal sealed record Recorded(string Method, Uri Uri, Dictionary<string, string> Headers, string? Body);

    internal sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
        public List<Recorded> Requests { get; } = new();

        public StubHandler Enqueue(int status, string? body, IDictionary<string, string>? headers = null)
        {
            _responses.Enqueue(_ => Response(status, body, headers));
            return this;
        }

        public StubHandler EnqueueFixture(string file, int status = 200, IDictionary<string, string>? headers = null) =>
            Enqueue(status, Fixture(file), headers);

        public StubHandler EnqueueThrow(Exception e)
        {
            _responses.Enqueue(_ => throw e);
            return this;
        }

        public static string Fixture(string file) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", file));

        private static HttpResponseMessage Response(int status, string? body, IDictionary<string, string>? headers)
        {
            var r = new HttpResponseMessage((HttpStatusCode)status);
            r.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body ?? ""));
            if (body != null && body.Length > 0)
                r.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
            r.Headers.TryAddWithoutValidation("X-Request-Id", "req-fixture-0001");
            r.Headers.TryAddWithoutValidation("API-Version", "v1");
            if (headers != null)
                foreach (var h in headers)
                {
                    r.Headers.Remove(h.Key);
                    r.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }
            return r;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in request.Headers) headers[h.Key] = string.Join(", ", h.Value);
            string? body = null;
            if (request.Content != null)
            {
                foreach (var h in request.Content.Headers) headers[h.Key] = string.Join(", ", h.Value);
                body = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            Requests.Add(new Recorded(request.Method.Method, request.RequestUri!, headers, body));
            if (_responses.Count == 0) throw new InvalidOperationException("No stubbed response left for " + request.RequestUri);
            return _responses.Dequeue()(request);
        }
    }

    internal static class TestClient
    {
        public static (AyInboxClient Client, StubHandler Stub, List<TimeSpan> Waits) Create(int maxRetries = 2)
        {
            var stub = new StubHandler();
            var waits = new List<TimeSpan>();
            var client = new AyInboxClient(new AyInboxClientOptions
            {
                ApiKey = "aybox_test_key_not_real_0000000000000000000000",
                BaseUrl = "https://api.test.invalid/v1/",
                HttpClient = new HttpClient(stub),
                MaxRetries = maxRetries,
            });
            client.Delay = (d, _) => { waits.Add(d); return Task.CompletedTask; };
            return (client, stub, waits);
        }
    }
}
