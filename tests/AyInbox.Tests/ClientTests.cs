using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AyInbox.Tests
{
    public class ClientTests
    {
        [Fact]
        public async Task SendsAuthUserAgentAndAccept()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("me.json");
            var me = await client.Me.GetAsync();

            Assert.Equal("dana@example.com", me.Email);
            Assert.Equal("api_key", me.AuthType);
            Assert.Contains("write_contacts", me.Scopes);
            var r = stub.Requests.Single();
            Assert.Equal("GET", r.Method);
            Assert.Equal("https://api.test.invalid/v1/me", r.Uri.ToString());
            Assert.Equal("Bearer aybox_test_key_not_real_0000000000000000000000", r.Headers["Authorization"]);
            Assert.Equal("ayinbox-dotnet/" + AyInboxClient.Version, r.Headers["User-Agent"]);
            Assert.Equal("application/json", r.Headers["Accept"]);
            Assert.False(r.Headers.ContainsKey("Idempotency-Key"));
        }

        [Fact]
        public void ToStringRedactsKey()
        {
            var (client, _, _) = TestClient.Create();
            Assert.DoesNotContain("aybox_test_key_not_real", client.ToString());
        }

        [Fact]
        public void MissingKeyThrows()
        {
            var old = Environment.GetEnvironmentVariable("AYINBOX_API_KEY");
            Environment.SetEnvironmentVariable("AYINBOX_API_KEY", null);
            try { Assert.Throws<AyInboxException>(() => new AyInboxClient()); }
            finally { Environment.SetEnvironmentVariable("AYINBOX_API_KEY", old); }
        }

        [Theory]
        [InlineData("errors/400_bad_request.json", 400, typeof(BadRequestException), "bad_request")]
        [InlineData("errors/400_invalid_cursor.json", 400, typeof(BadRequestException), "bad_request")]
        [InlineData("errors/401_unauthorized.json", 401, typeof(AuthenticationException), "unauthorized")]
        [InlineData("errors/401_api_key_holder_left.json", 401, typeof(AuthenticationException), "api_key_holder_left")]
        [InlineData("errors/402_insufficient_credits.json", 402, typeof(PaymentRequiredException), "insufficient_credits")]
        [InlineData("errors/403_forbidden.json", 403, typeof(PermissionDeniedException), "forbidden")]
        [InlineData("errors/403_api_key_mailbox_limited.json", 403, typeof(PermissionDeniedException), "api_key_mailbox_limited")]
        [InlineData("errors/404_not_found.json", 404, typeof(NotFoundException), "not_found")]
        [InlineData("errors/409_conflict.json", 409, typeof(ConflictException), "conflict")]
        [InlineData("errors/409_idempotency_outcome_unknown.json", 409, typeof(ConflictException), "idempotency_outcome_unknown")]
        [InlineData("errors/409_idempotent_secret_withheld.json", 409, typeof(ConflictException), "idempotent_secret_withheld")]
        [InlineData("errors/422_unprocessable.json", 422, typeof(UnprocessableException), "unprocessable")]
        [InlineData("errors/429_api_key_rate_limited.json", 429, typeof(RateLimitException), "rate_limit_exceeded")]
        [InlineData("errors/500_internal_error.json", 500, typeof(InternalServerException), "internal_error")]
        [InlineData("errors/503_service_unavailable.json", 503, typeof(ServiceUnavailableException), "service_unavailable")]
        public async Task MapsErrorEnvelopes(string fixture, int status, Type type, string code)
        {
            var (client, stub, _) = TestClient.Create(maxRetries: 0);
            stub.EnqueueFixture(fixture, status);
            var e = await Assert.ThrowsAnyAsync<ApiException>(() => client.Campaigns.GetAsync("11111111-1111-4111-8111-111111111111"));
            Assert.IsType(type, e);
            Assert.Equal(status, e.Status);
            Assert.Equal(code, e.Code);
            Assert.Equal("req-fixture-0001", e.RequestId);
            Assert.False(string.IsNullOrEmpty(e.ApiMessage));
            Assert.DoesNotContain("aybox_", e.Message);
        }

        [Fact]
        public async Task ValidationParentCoversBothStatuses()
        {
            var (client, stub, _) = TestClient.Create(maxRetries: 0);
            stub.EnqueueFixture("errors/422_unprocessable.json", 422);
            await Assert.ThrowsAnyAsync<ValidationException>(() => client.Me.GetAsync());
        }

        [Fact]
        public async Task RateLimitWithoutCodeUsesRetryAfterMs()
        {
            var (client, stub, _) = TestClient.Create(maxRetries: 0);
            stub.Enqueue(429, StubHandler.Fixture("errors/429_category_rate_limited.json"));
            var e = await Assert.ThrowsAsync<RateLimitException>(() => client.Me.GetAsync());
            Assert.Equal("rate_limit_exceeded", e.Code);
            Assert.Equal(12, e.RetryAfterSeconds);
            Assert.Equal("req-fixture-0001", e.RequestId); // from the header
        }

        [Fact]
        public async Task NonJsonErrorKeepsTextAndDefaultCode()
        {
            var (client, stub, _) = TestClient.Create(maxRetries: 0);
            stub.Enqueue(502, "<html>bad gateway</html>");
            var e = await Assert.ThrowsAsync<InternalServerException>(() => client.Me.GetAsync());
            Assert.Equal("internal_error", e.Code);
            Assert.Equal("<html>bad gateway</html>", e.Body);
        }

        [Fact]
        public async Task RetriesOn503ThenSucceeds()
        {
            var (client, stub, waits) = TestClient.Create();
            stub.EnqueueFixture("errors/503_service_unavailable.json", 503).EnqueueFixture("me.json");
            var me = await client.Me.GetAsync();
            Assert.Equal("dana@example.com", me.Email);
            Assert.Equal(2, stub.Requests.Count);
            Assert.Single(waits);
            Assert.InRange(waits[0].TotalSeconds, 0, 0.5);
        }

        [Fact]
        public async Task RetriesOn429HonouringRetryAfter()
        {
            var (client, stub, waits) = TestClient.Create();
            stub.EnqueueFixture("errors/429_api_key_rate_limited.json", 429, new Dictionary<string, string> { ["Retry-After"] = "3" })
                .EnqueueFixture("campaign.json");
            var c = await client.Campaigns.GetAsync("11111111-1111-4111-8111-111111111111");
            Assert.False(string.IsNullOrEmpty(c.Id));
            Assert.Equal(TimeSpan.FromSeconds(3), waits.Single());
        }

        [Fact]
        public async Task RetryAfterIsCapped()
        {
            var (client, stub, waits) = TestClient.Create();
            stub.EnqueueFixture("errors/429_api_key_rate_limited.json", 429, new Dictionary<string, string> { ["Retry-After"] = "9999" })
                .EnqueueFixture("me.json");
            await client.Me.GetAsync();
            Assert.Equal(TimeSpan.FromSeconds(120), waits.Single());
        }

        [Fact]
        public async Task GivesUpAfterMaxRetries()
        {
            var (client, stub, waits) = TestClient.Create(maxRetries: 2);
            for (var i = 0; i < 3; i++) stub.EnqueueFixture("errors/503_service_unavailable.json", 503);
            await Assert.ThrowsAsync<ServiceUnavailableException>(() => client.Me.GetAsync());
            Assert.Equal(3, stub.Requests.Count);
            Assert.Equal(2, waits.Count);
        }

        [Fact]
        public async Task DoesNotRetryKeylessPostOn503()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("errors/503_service_unavailable.json", 503).EnqueueFixture("campaign.json");
            await Assert.ThrowsAsync<ServiceUnavailableException>(() => client.Campaigns.CreateAsync(new CampaignCreate { Name = "x" }));
            Assert.Single(stub.Requests);
        }

        [Fact]
        public async Task DoesNotRetryKeylessPostOnConnectionError()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueThrow(new HttpRequestException("reset")).EnqueueFixture("campaign.json");
            await Assert.ThrowsAsync<ConnectionException>(() => client.Campaigns.CreateAsync(new CampaignCreate { Name = "x" }));
            Assert.Single(stub.Requests);
        }

        [Fact]
        public async Task RetriesKeylessPostOn429()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("errors/429_api_key_rate_limited.json", 429, new Dictionary<string, string> { ["Retry-After"] = "0" })
                .EnqueueFixture("campaign.json");
            await client.Campaigns.CreateAsync(new CampaignCreate { Name = "x" });
            Assert.Equal(2, stub.Requests.Count);
        }

        [Fact]
        public async Task PostWithIdempotencyKeyRetriesAndReusesKey()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("errors/503_service_unavailable.json", 503).EnqueueFixture("contacts_create.json");
            var created = await client.Contacts.CreateAsync(new ContactInput { Email = "lee@example.com", FirstName = "Lee" },
                new RequestOptions { IdempotencyKey = "import-lee-1" });
            Assert.Equal("lee@example.com", created[0].Email);
            Assert.Equal(2, stub.Requests.Count);
            Assert.All(stub.Requests, r => Assert.Equal("import-lee-1", r.Headers["Idempotency-Key"]));
            Assert.Equal("application/json", stub.Requests[0].Headers["Content-Type"]);
            using var body = JsonDocument.Parse(stub.Requests[0].Body!);
            Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
            Assert.Equal("Lee", body.RootElement[0].GetProperty("first_name").GetString());
            Assert.False(body.RootElement[0].TryGetProperty("company", out _));
        }

        [Fact]
        public async Task DoesNotRetryOutcomeUnknown()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("errors/409_idempotency_outcome_unknown.json", 409);
            await Assert.ThrowsAsync<ConflictException>(() => client.Campaigns.PauseAsync("c1", new RequestOptions { IdempotencyKey = "k" }));
            Assert.Single(stub.Requests);
        }

        [Fact]
        public async Task TimeoutBecomesTimeoutException()
        {
            var stub = new SlowHandler();
            using var client = new AyInboxClient(new AyInboxClientOptions
            {
                ApiKey = "aybox_x", BaseUrl = "https://api.test.invalid/v1", HttpClient = new HttpClient(stub),
                Timeout = TimeSpan.FromMilliseconds(50), MaxRetries = 0,
            });
            await Assert.ThrowsAsync<AyInboxTimeoutException>(() => client.Me.GetAsync());
        }

        private sealed class SlowHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken ct)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            }
        }

        [Fact]
        public async Task InjectedHttpClientIsNotDisposed()
        {
            var stub = new StubHandler();
            var http = new HttpClient(stub);
            var client = new AyInboxClient(new AyInboxClientOptions { ApiKey = "aybox_x", HttpClient = http });
            client.Dispose();
            stub.EnqueueFixture("me.json");
            var res = await http.GetAsync("https://api.test.invalid/v1/me");
            Assert.True(res.IsSuccessStatusCode);
        }

        [Fact]
        public async Task GenericRequestHandlesJsonEmptyAndQuery()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("unibox_count.json").Enqueue(200, "");
            var count = await client.RequestAsync("GET", "/unibox/count",
                new Dictionary<string, string?> { ["email_id"] = "a b", ["skip"] = null });
            Assert.True(count.HasValue);
            Assert.Equal(JsonValueKind.Number, count!.Value.GetProperty("count").ValueKind);
            Assert.Equal("https://api.test.invalid/v1/unibox/count?email_id=a%20b", stub.Requests[0].Uri.AbsoluteUri);

            var empty = await client.RequestAsync("DELETE", "/campaigns/c1/steps/s1", options: new RequestOptions { IdempotencyKey = "del-1" });
            Assert.Null(empty);
        }

        [Fact]
        public async Task PaginatesAcrossTwoPages()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("campaigns_list.json").EnqueueFixture("campaigns_list_page2.json");
            var names = new List<string>();
            await foreach (var c in client.Campaigns.ListAllAsync(new CampaignListParams { Limit = 2 })) names.Add(c.Name);
            Assert.Equal(3, names.Count);
            Assert.Equal(2, stub.Requests.Count);
            Assert.Equal("?limit=2", stub.Requests[0].Uri.Query);
            Assert.Equal("?limit=2&cursor=c1_ZmFrZS1jdXJzb3ItcGFnZS0y", stub.Requests[1].Uri.Query);
        }

        [Fact]
        public async Task GenericPaginateWalksRows()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("campaigns_list.json").EnqueueFixture("campaigns_list_page2.json");
            var n = 0;
            await foreach (var row in client.PaginateAsync("/campaigns"))
            {
                Assert.Equal(JsonValueKind.Object, row.ValueKind);
                n++;
            }
            Assert.Equal(3, n);
        }

        [Fact]
        public async Task PaginationWaitsOutA429MidWalk()
        {
            var (client, stub, waits) = TestClient.Create();
            stub.EnqueueFixture("campaigns_list.json")
                .EnqueueFixture("errors/429_api_key_rate_limited.json", 429, new Dictionary<string, string> { ["Retry-After"] = "1" })
                .EnqueueFixture("campaigns_list_page2.json");
            var n = 0;
            await foreach (var _ in client.Campaigns.ListAllAsync()) n++;
            Assert.Equal(3, n);
            Assert.Single(waits);
        }

        [Fact]
        public async Task UnknownFieldsAreKeptInExtra()
        {
            var (client, stub, _) = TestClient.Create();
            stub.Enqueue(200, "{\"user_id\":\"u\",\"email\":\"e@example.com\",\"scopes\":[],\"brand_new_field\":{\"x\":1}}");
            var me = await client.Me.GetAsync();
            Assert.True(me.Extra!.ContainsKey("brand_new_field"));
        }
    }
}
