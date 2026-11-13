# Changelog

## 0.1.0 (unreleased)

- First release: `Me`, `Campaigns` (list, get, create, pause), `Contacts` (list, get, create, update, delete), `Mailboxes.List`, `Unibox` (list threads, get thread).
- Generic `RequestAsync`, `RequestRawAsync` and `PaginateAsync` for every other route.
- `IAsyncEnumerable` auto-pagers, typed exceptions, retries with backoff that honour `Retry-After`, per-call idempotency keys.
- Webhook signature verification with a constant-time compare.
- Targets `net8.0` and `netstandard2.0`.
