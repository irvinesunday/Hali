# PR #56 — Npgsql Enum Registration Fix Notes

## Summary of the fix

PR #56 wraps every `UseNpgsql` call with a centrally-built `NpgsqlDataSource`
that maps all 7 custom Hali PostgreSQL enums (`account_type`, `auth_method`,
`signal_state`, `participation_type`, `official_post_type`,
`location_precision_type`, `civic_category`). Previously enums were being
registered on the EF `NpgsqlDbContextOptionsBuilder`, which is no longer
sufficient — Npgsql requires explicit `MapEnum<T>` registration on the
data source itself, otherwise any DB write involving a custom enum column
throws `NotSupportedException` at runtime. This was breaking the OTP flow
end-to-end. A shared `HaliNpgsqlDataSourceFactory` is now the single source
of truth, applied to both runtime DI wiring and the design-time
`IDesignTimeDbContextFactory` implementations so EF migrations also pick up
the mappings. The data sources are owned by a singleton `HaliDataSources`
holder registered with DI so the host disposes the connection pools on
shutdown.

## Copilot comments addressed

### Comment 1
- **File:** src/Hali.Infrastructure/Signals/NominatimGeocodingService.cs
- **Line:** 79
- **Comment:** `System.Web.HttpUtility` isn't available by default on net10.0 and this project doesn't reference the `System.Web.HttpUtility` package, so this `using System.Web;` / `HttpUtility.UrlEncode` usage is likely to fail compilation. Use `Uri.EscapeDataString(...)` instead.
- **Fix applied:** Removed `using System.Web;` and replaced `HttpUtility.UrlEncode(query.Trim())` with `Uri.EscapeDataString(query.Trim())`. No extra package dependency required.

### Comment 2
- **File:** src/Hali.Infrastructure/Signals/NominatimGeocodingService.cs
- **Line:** 122
- **Comment:** `HttpResponseMessage response` is never disposed in `SearchAsync`. Use `using var response = await _http.SendAsync(...)`.
- **Fix applied:** Restructured `SearchAsync` so the response is captured with `using var response = await _http.SendAsync(req, ct);` inside a single `try` block that also covers JSON parsing and the upstream call, with one shared `catch` that returns an empty result. This guarantees disposal on every code path.

### Comment 3
- **File:** src/Hali.Api/Controllers/LocalitiesController.cs
- **Line:** 70
- **Comment:** `DisplayLabel` is persisted to a column with `HasMaxLength(160)`, but the request pipeline doesn't validate or truncate `dto.Items[*].DisplayLabel`. Enforce max length at the API boundary.
- **Fix applied:** Added `[StringLength(160)]` to `FollowedLocalityItemDto.DisplayLabel` in `src/Hali.Contracts/Notifications/FollowedLocalitiesRequestDto.cs`. Because the controller is `[ApiController]`, ASP.NET Core's automatic model validation now returns a 400 with a validation problem details payload when an over-length label is sent, before reaching the database.

### Comment 4
- **File:** src/Hali.Api/Controllers/LocalitiesController.cs
- **Line:** 99
- **Comment:** Anonymous search endpoint can trigger external HTTP calls + PostGIS queries. Add a maximum query length and rate limiting/throttling for anonymous callers.
- **Fix applied:** Added `MaxSearchQueryLength = 80` (returns 400 `query_too_long` when exceeded) and integrated the existing `IRateLimiter` (Redis-backed) keyed by `ratelimit:locality_search:{clientIp}` with a budget of 30 requests per minute, returning HTTP 429 when exhausted. The rate limiter is injected into the controller via DI alongside the existing dependencies.

### Comment 5
- **File:** src/Hali.Api/Controllers/LocalitiesController.cs
- **Line:** 150
- **Comment:** Comment says "Keep the first 2–3 segments" but the implementation always keeps 2 segments (`parts.Take(2)`). Update the comment or the logic so they match.
- **Fix applied:** Updated the comment to "Keep the first 2 segments which give the area + city context." matching the existing `parts.Take(2)` logic. The 2-segment trim is the correct product behavior, so the code was kept and the comment corrected.

### Comment 6
- **File:** src/Hali.Infrastructure/Extensions/ServiceCollectionExtensions.cs
- **Line:** 42
- **Comment:** `NpgsqlDataSource` instances are constructed inside `AddInfrastructure` and captured in lambdas, but they aren't registered with DI, so the host won't dispose them on shutdown. Register each data source as a singleton so it's disposed with the ServiceProvider.
- **Fix applied:** Introduced `HaliDataSources` (in `src/Hali.Infrastructure/Data/HaliDataSources.cs`) — a single owning holder that exposes one `NpgsqlDataSource` per DbContext (Auth, Signals, Clusters, Participation, Advisories, Notifications) and implements both `IDisposable` and `IAsyncDisposable`. The holder is registered as a singleton via `services.AddSingleton(dataSources)` so the DI container disposes the underlying connection pools on host shutdown. Each `AddDbContext` registration now uses the `(sp, opts)` overload and resolves its data source from `sp.GetRequiredService<HaliDataSources>()`. This avoids the need for keyed services while still giving DI ownership of every data source.

### Comment 7
- **File:** 02_openapi.yaml
- **Line:** 241
- **Comment:** PR description focuses on Npgsql enum mapping but this PR also introduces `/v1/localities/search`, changes `/v1/localities/followed`, adds `display_label` persistence and a migration, and updates the mobile client. Update the PR description/scope or split.
- **Fix applied:** Documented the broader scope here in `docs/arch/pr56-enum-fix-notes.md` (this file) and acknowledged that the branch `fix/npgsql-enum-registration` was created on top of the existing `fix/locality-display-names` branch, which is why it inherits those changes. Splitting the branch would require rebasing already-reviewed locality work, so the scope expansion is intentional and the PR description has been clarified to call out both workstreams: (a) enum registration hardening and (b) the locality display-label / search work that was already in progress on the parent branch. No code change in `02_openapi.yaml`.

## Files changed

- `src/Hali.Infrastructure/Data/HaliNpgsqlDataSourceFactory.cs` — new shared factory that builds an `NpgsqlDataSource` with all 7 custom enums (and optional NetTopologySuite) registered.
- `src/Hali.Infrastructure/Data/HaliDataSources.cs` — new singleton holder that owns and disposes one `NpgsqlDataSource` per DbContext.
- `src/Hali.Infrastructure/Extensions/ServiceCollectionExtensions.cs` — builds the data sources, registers `HaliDataSources` as a singleton, and switches every `AddDbContext` to resolve its data source from DI for proper disposal.
- `src/Hali.Infrastructure/Data/Auth/AuthDbContextFactory.cs`, `…/Clusters/ClustersDbContextFactory.cs`, `…/Signals/SignalsDbContextFactory.cs`, `…/Admin/AdminDbContextFactory.cs` — design-time factories use the same `HaliNpgsqlDataSourceFactory` so EF migrations also pick up enum mappings.
- `src/Hali.Infrastructure/Signals/NominatimGeocodingService.cs` — replaced `HttpUtility.UrlEncode` with `Uri.EscapeDataString` and ensured `HttpResponseMessage` is disposed via `using var response = …` in `SearchAsync`.
- `src/Hali.Api/Controllers/LocalitiesController.cs` — added `MaxSearchQueryLength` cap, IP-based rate limiting on `/v1/localities/search`, and corrected the `TrimLabel` comment to match the 2-segment behavior. Constructor now takes `IRateLimiter`.
- `src/Hali.Contracts/Notifications/FollowedLocalitiesRequestDto.cs` — added `[StringLength(160)]` to `DisplayLabel` so over-length input returns 400 instead of a 500 from the database.
- `docs/arch/pr56-enum-fix-notes.md` — this document.

## Verification

```bash
# Build
dotnet build src/Hali.Api/Hali.Api.csproj
# → Build succeeded, 0 errors

# Manual smoke test (requires the local Postgres + Redis stack)
dotnet run --project src/Hali.Api
# In another terminal:
curl -X POST http://localhost:8080/v1/auth/otp \
     -H 'Content-Type: application/json' \
     -d '{"phoneNumber":"+254700000000","idempotencyKey":"test-1"}'
# → Expect 200, no NotSupportedException, otp_challenges row written.

# Locality search rate-limit check
for i in $(seq 1 35); do
  curl -s -o /dev/null -w "%{http_code}\n" \
    "http://localhost:8080/v1/localities/search?q=westlands"
done
# → First ~30 calls return 200, subsequent calls return 429.
```
