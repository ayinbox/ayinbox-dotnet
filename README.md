# ayinbox-dotnet

The official .NET client for the AY Inbox REST API. It targets `net8.0` and `netstandard2.0`, and is built on `HttpClient` and `System.Text.Json`.

AY Inbox is a hosted cold email platform: it sends cold email at a paced daily volume, brings every reply into one inbox (Unibox), drafts answers from a per-campaign brain, and can offer a meeting time from Cal.com or Calendly. Access is invite only for now: ask at team@ayinbox.com.

AY Inbox is a hosted service. This repository holds the .NET client library for its public API. The service itself is not open source.

## Install

Not on NuGet yet. The package will be `AyInbox`.

Until then, reference it from a clone of this repository:

```sh
git clone https://github.com/ayinbox/ayinbox-dotnet.git
dotnet add <your project>.csproj reference ayinbox-dotnet/src/AyInbox/AyInbox.csproj
```

## Quick start

```csharp
using AyInbox;

using var client = new AyInboxClient(); // reads AYINBOX_API_KEY

var me = await client.Me.GetAsync();
Console.WriteLine($"Signed in as {me.Email} with scopes {string.Join(", ", me.Scopes)}");

await foreach (var campaign in client.Campaigns.ListAllAsync())
    Console.WriteLine($"{campaign.Status}  {campaign.Name}");

try
{
    var created = await client.Contacts.CreateAsync(
        new ContactInput { Email = "lee@example.com", FirstName = "Lee" },
        new RequestOptions { IdempotencyKey = "import-2026-11-13-lee" });
    Console.WriteLine($"Contact {created[0].Id}");
}
catch (PermissionDeniedException e)
{
    Console.WriteLine($"Missing scope: {e.Code} (request {e.RequestId})");
}
```

Create an API key in the dashboard at https://app.ayinbox.com. Keys start with `aybox_`; OAuth access tokens (`ayat_`) work the same way.

## Client options

```csharp
var client = new AyInboxClient(new AyInboxClientOptions
{
    ApiKey = "aybox_...",                     // default: AYINBOX_API_KEY
    BaseUrl = "https://api.ayinbox.com/v1",   // default: AYINBOX_BASE_URL, then this
    Timeout = TimeSpan.FromSeconds(30),       // per attempt
    MaxRetries = 2,                           // 0 disables retries
    HttpClient = myHttpClient,                // optional; never disposed by the client
});
```

Every method takes a `RequestOptions` (`IdempotencyKey`, `Timeout`, `Headers`, `MaxRetries`) and a `CancellationToken`. The key is never logged, and `ToString()` hides it. When you pass your own `HttpClient` (for example from `IHttpClientFactory`), the client uses it as is and does not dispose it.

## Resources

| Resource | Methods | Route |
|---|---|---|
| `Me` | `GetAsync` | `GET /me` |
| `Campaigns` | `ListAsync`, `ListAllAsync` | `GET /campaigns` |
| | `GetAsync` | `GET /campaigns/{id}` |
| | `CreateAsync` | `POST /campaigns` |
| | `PauseAsync` | `POST /campaigns/{id}/stop` |
| `Contacts` | `ListAsync`, `ListAllAsync` | `POST /contacts/search` |
| | `GetAsync` | `GET /contacts/{id}` |
| | `CreateAsync` | `POST /contacts` |
| | `UpdateAsync` | `PATCH /contacts/{id}` |
| | `DeleteAsync` | `DELETE /contacts/{id}` |
| `Mailboxes` | `ListAsync`, `ListAllAsync` | `GET /emails` |
| `Unibox` | `ListThreadsAsync`, `ListAllThreadsAsync` | `GET /unibox` |
| | `GetThreadAsync`, `GetThreadAllAsync` | `GET /unibox/thread` |
| `WebhookSignature` | `Verify`, `ConstructEvent` | local, no HTTP |

Every other route is reachable through `RequestAsync`:

```csharp
var count = await client.RequestAsync("GET", "/unibox/count");
var steps = await client.RequestAsync("GET", $"/campaigns/{id}/steps");
var result = await client.RequestAsync("POST", "/suppressions",
    body: new { entries = new[] { new { value = "example.org" } } },
    options: new RequestOptions { IdempotencyKey = "suppress-example-org" });
```

`RequestAsync` returns a `JsonElement?` (null for an empty body). `RequestRawAsync` returns status, headers and bytes.

Response fields a model does not type land in its `Extra` dictionary, so new API fields never break deserialization. Timestamps are `DateTimeOffset`. Fields whose shape the API does not fix (lead scores, engagement, schedule windows, webhook `data`) are `JsonElement`. `Page<T>.Raw` keeps the whole list response, including extras such as `counts` on contact search.

## Errors

Every API error is an `ApiException` with `Status`, `Code`, `ApiMessage`, `Reason`, `RequestId`, `Body`, `Headers` and `RetryAfterSeconds`. Branch on `Status` and `Code`, never on the message.

| Exception | Status |
|---|---|
| `BadRequestException` (a `ValidationException`) | 400 |
| `AuthenticationException` | 401 |
| `PaymentRequiredException` | 402 |
| `PermissionDeniedException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `UnprocessableException` (a `ValidationException`) | 422 |
| `RateLimitException` | 429 |
| `InternalServerException` (a `ServerException`) | 500 and other 5xx |
| `ServiceUnavailableException` (a `ServerException`) | 503 |
| `ConnectionException`, `AyInboxTimeoutException` (both `NetworkException`) | no response |

All of them extend `AyInboxException`. A 429 from a per-user limiter carries no `code` in its body; the client still reports `rate_limit_exceeded` and reads the wait from `Retry-After`, `retry_after` or `retry_after_ms`.

## Retries and timeouts

The client retries 429, 502, 503, 504 and network failures up to `MaxRetries` times (default 2). It waits for `Retry-After` when the API sends it (capped at 120 seconds), otherwise exponential backoff with full jitter (0.5 s base, 8 s cap).

A `POST` or `PATCH` without an idempotency key is retried only on 429, which the API never runs. Pass an idempotency key to let it retry other failures. `Contacts.ListAsync` is a read sent as a `POST` and is retried like a `GET`. A 500 and `409 idempotency_outcome_unknown` are never retried.

The default timeout is 30 seconds per attempt. Override it per client or per call with `RequestOptions.Timeout`. The `CancellationToken` cancels the call and any wait between retries.

## Pagination

List methods return one `Page<T>` with `Data`, `NextCursor`, `HasMore` and `Total` (not on every list). The `ListAll` variants return an `IAsyncEnumerable<T>` that follows `next_cursor` until `has_more` is false:

```csharp
await foreach (var contact in client.Contacts.ListAllAsync(new ContactSearch { CampaignIds = new() { campaignId } }))
    Console.WriteLine(contact.Email);
```

For any other paginated route, `client.PaginateAsync("/suppressions")` yields each row as a `JsonElement`. Recommended page sizes are 1 to 100.

## Idempotency

Every mutating call accepts `new RequestOptions { IdempotencyKey = "..." }`, sent as `Idempotency-Key` (1 to 255 visible ASCII characters). The API stores the result for 24 hours: the same key and request replays the original answer, and the same key with a different request answers `409 conflict`. If the first attempt ended without an outcome, the key answers `409 idempotency_outcome_unknown` for 24 hours: find out whether the change happened before trying again with a new key. The client does not generate keys for you; every retry of a call reuses the key you gave it.

## Webhooks

```csharp
var ok = WebhookSignature.Verify(
    rawBodyBytes,                                   // the raw request body, before JSON parsing
    request.Headers["X-Ayinbox-Signature"],
    "whsec_...");                                   // the endpoint secret, prefix included

// or verify and parse in one step, throwing SignatureVerificationException:
var ev = WebhookSignature.ConstructEvent(rawBodyBytes, header, secret);
Console.WriteLine($"{ev.EventType} {ev.Id}");
```

The signature is HMAC-SHA256 over `"<t>.<raw body>"`, compared in constant time. A timestamp more than 300 seconds from now fails by default. Use the `X-Ayinbox-Event-Id` header (equal to `ev.Id`) to drop retried deliveries. In ASP.NET Core, read the body with `Request.EnableBuffering()` or into a `MemoryStream` before any model binding.

## Calls that send email

These calls put real email on the wire:

- `campaigns.start` (`POST /campaigns/{id}/start`, through `RequestAsync`): needs `START_CAMPAIGNS` or `SEND_CAMPAIGNS`.
- `campaigns.update` with `status: "active"` (and `completed` or `paused_undeliverable`), through `RequestAsync`: needs `START_CAMPAIGNS` or `SEND_CAMPAIGNS` in addition to `WRITE_CAMPAIGNS`.
- `campaigns.leads.resume` on a finished campaign (`POST /campaigns/{id}/leads/{contactId}/resume`, through `RequestAsync`): needs `START_CAMPAIGNS` or `SEND_CAMPAIGNS`.
- `unibox.reply` and `unibox.approveAgentDraft` (`POST /unibox/reply`, `POST /unibox/agent-drafts/{id}/approve`, through `RequestAsync`): need `WRITE_UNIBOX`. A key with `WRITE_UNIBOX` can send replies from any mailbox it can reach.
- Also through `RequestAsync`: `POST /unibox/compose`, `POST /emails/{id}/send`, `POST /campaigns/{id}/test-email`, the first-email review queue (`/campaigns/{id}/reviews/...`) and placement tests need `SEND_CAMPAIGNS` (compose needs `WRITE_UNIBOX`).

Adding contacts can send mail without calling start. Contacts added to an `active` campaign (through `Contacts.CreateAsync` with `Campaigns`) are queued; a `completed` campaign starts again on its own when leads are added; a contact that comes to match a segment linked to a sending campaign is enrolled.

`START_CAMPAIGNS` starts, resumes and pauses campaigns and nothing else. `SEND_CAMPAIGNS` covers everything `START_CAMPAIGNS` covers plus the other sending actions. `WRITE_CAMPAIGNS` alone edits drafts and pauses, and cannot start anything. `Campaigns.CreateAsync` never sends and `Campaigns.PauseAsync` sends nothing.

A key for a model that reads the inbox should use the "Agent: read and draft" preset (84574239): every read scope plus `AI_DRAFT`, with nothing that writes or sends.

Some calls spend workspace credits (AI drafts, research, enrichment, paid tools). They answer `402 insufficient_credits` when the balance is out.

Retrying a send without an idempotency key can send twice. Pass an idempotency key on every mutating call you might retry.

## Examples

`examples/` holds two programs: `dotnet run --project examples/ListCampaigns` (needs `AYINBOX_API_KEY`) and `dotnet run --project examples/VerifyWebhook` (runs offline against the test vector).

## Development

Tests use xUnit with a stub `HttpMessageHandler` fed from `tests/AyInbox.Tests/fixtures`, with no network and no real key:

```sh
dotnet test
```

## Links

- API reference: https://docs.ayinbox.com/api/
- Dashboard: https://app.ayinbox.com
- Contact: team@ayinbox.com

## License

MIT. See `LICENSE`.
