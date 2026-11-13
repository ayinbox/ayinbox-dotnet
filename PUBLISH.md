# Publishing to NuGet

`AyInbox` publishes to nuget.org through NuGet trusted publishing: the release workflow exchanges a GitHub OIDC token for a short-lived API key with `NuGet/login`. No long-lived NuGet key is stored anywhere.

## One-time setup

1. **nuget.org account.** Sign in at https://www.nuget.org with the AY Inbox team account (a Microsoft account on team@ayinbox.com). If you want the package owned by an organization, create the `AyInbox` organization under Manage organizations and publish as it.
2. **Reserve the package id (optional).** The first push claims `AyInbox`. To also get the verified prefix mark, request an ID prefix reservation for `AyInbox` (see https://learn.microsoft.com/nuget/nuget-org/id-prefix-reservation).
3. **Trusted publishing policy.** On nuget.org, open your user (or organization) menu, then Trusted Publishing, and add a policy:
   - Repository owner: `ayinbox`
   - Repository: `ayinbox-dotnet`
   - Workflow file: `release.yml`
   - Environment: `nuget`
4. **GitHub environment.** In `ayinbox/ayinbox-dotnet`, Settings, Environments, create `nuget` (optionally limit it to `v*` tags and add required reviewers). Add an environment variable (not a secret) `NUGET_USER` set to the nuget.org user name or organization name that owns the policy.

Nothing else is needed: `release.yml` asks GitHub for an OIDC token, `NuGet/login` trades it for a one-hour key, and `dotnet nuget push` uses it.

## Releasing

1. Set `<Version>` in `src/AyInbox/AyInbox.csproj` and `AyInboxClient.Version` to the new version, and add it to `CHANGELOG.md`.
2. Commit, then tag and push:
   ```sh
   git tag v0.1.0
   git push origin v0.1.0
   ```
3. The `Release` workflow checks the tag matches the version, runs the tests, packs and pushes `AyInbox` with its symbols package. The package appears on nuget.org after validation and indexing (usually a few minutes).
4. Update the README install section to `dotnet add package AyInbox` once it is live.
