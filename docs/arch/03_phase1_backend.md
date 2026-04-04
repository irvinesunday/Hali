# Hali — Phase 1 Backend Implementation Guide
**Claude Code implementation prompt for the backend API and workers.**

Read `CLAUDE.md`, `docs/arch/01_schema_reference.md`, and `docs/arch/02_api_contracts.md` before starting any work in this guide.

---

## Build order

Do not jump ahead. Complete each phase gate before moving to the next.

```
Gate 1: Repo skeleton + local stack running
Gate 2: Schema migrations applied and verified
Gate 3: Auth endpoints working end-to-end with real OTP
Gate 4: Signal preview + submit endpoints working with NLP extraction
Gate 5: Clustering + CIVIS activation working
Gate 6: Participation endpoints working with idempotency
Gate 7: Official updates endpoints working
Gate 8: Restoration lifecycle working end-to-end
Gate 9: Notifications wired
Gate 10: All integration tests passing
```

---

## Gate 1 — Repository skeleton

### What to create

```
/src
  /Hali.Api/
    Program.cs
    appsettings.json
    appsettings.Development.json
    Controllers/
    Middleware/
      RequestLoggingMiddleware.cs
      IdempotencyMiddleware.cs
    Filters/
  /Hali.Workers/
    Program.cs
    Workers/
  /Hali.Domain/
    Entities/
    ValueObjects/
    Enums/
      CivicCategory.cs       -- enum matching the 8-category set
      SignalState.cs
      ParticipationType.cs
      OfficialPostType.cs
    Rules/                   -- pure domain rules, no infrastructure
  /Hali.Application/
    Commands/
    Queries/
    DTOs/
    Interfaces/
      INlpExtractionService.cs
      ISmsProvider.cs
      IGeocodingService.cs
      IPushNotificationService.cs
  /Hali.Infrastructure/
    Persistence/
      HaliDbContext.cs
      Configurations/        -- EF entity configurations
      Migrations/
    Repositories/
    External/
      Nlp/                   -- Anthropic API adapter
      Sms/                   -- Africa's Talking adapter
      Geocoding/             -- Nominatim adapter
      Push/                  -- Expo Push adapter
    Services/
  /Hali.Contracts/
    Requests/
    Responses/
    Events/
```

### Program.cs requirements

```csharp
// Required registrations in order:
builder.Services.AddHaliAuth(builder.Configuration);
builder.Services.AddHaliDatabase(builder.Configuration);
builder.Services.AddHaliRedis(builder.Configuration);
builder.Services.AddHaliModules();           // registers all module services
builder.Services.AddHaliRateLimiting();
builder.Services.AddHaliObservability();
builder.Services.AddOpenApi();

// Middleware order (ORDER MATTERS):
app.UseRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseIdempotency();
app.MapControllers();
```

### appsettings.json structure

```json
{
  "ConnectionStrings": {
    "Postgres": "",
    "Redis": ""
  },
  "Jwt": {
    "Issuer": "hali-api",
    "Audience": "hali-platform",
    "Secret": "",
    "ExpiryMinutes": 60
  },
  "RefreshToken": {
    "ExpiryDays": 30
  },
  "Civis": {
    "MinUniqueDevices": 2,
    "JoinThreshold": 0.65,
    "RestorationRatio": 0.60,
    "MinRestorationAffectedVotes": 2,
    "ContextEditWindowMinutes": 2,
    "Categories": {
      "Roads": { "BaseFloor": 2, "HalfLifeHours": 18, "MacfMin": 2, "MacfMax": 6 },
      "Transport": { "BaseFloor": 2, "HalfLifeHours": 8, "MacfMin": 2, "MacfMax": 5 },
      "Electricity": { "BaseFloor": 2, "HalfLifeHours": 12, "MacfMin": 2, "MacfMax": 6 },
      "Water": { "BaseFloor": 2, "HalfLifeHours": 24, "MacfMin": 2, "MacfMax": 7 },
      "Environment": { "BaseFloor": 2, "HalfLifeHours": 36, "MacfMin": 2, "MacfMax": 6 },
      "Safety": { "BaseFloor": 2, "HalfLifeHours": 18, "MacfMin": 2, "MacfMax": 6 },
      "Infrastructure": { "BaseFloor": 2, "HalfLifeHours": 24, "MacfMin": 2, "MacfMax": 6 },
      "Governance": { "BaseFloor": 2, "HalfLifeHours": 24, "MacfMin": 2, "MacfMax": 6 }
    }
  },
  "Nlp": {
    "Model": "claude-sonnet-4-5",
    "TimeoutSeconds": 20
  },
  "Geocoding": {
    "Provider": "nominatim",
    "BaseUrl": "https://nominatim.openstreetmap.org",
    "UserAgent": "HaliMVP/1.0",
    "TimeoutSeconds": 10
  }
}
```

---

## Gate 2 — Schema migrations

### EF Core migration checklist

Apply migrations in this order. Each module creates its own migration file:

1. `Infrastructure_CoreExtensions` — PostGIS, uuid-ossp, pgcrypto
2. `Auth_Accounts` — accounts, devices, otp_challenges
3. `Auth_RefreshTokens` — refresh_tokens table (see schema reference)
4. `Geography_Localities` — localities, location_labels
5. `Taxonomy_Categories` — taxonomy_categories, taxonomy_conditions
6. `Taxonomy_Seed` — insert canonical subcategories and conditions
7. `Institutions_Core` — institutions, institution_jurisdictions
8. `Signals_Core` — signal_events, signal_clusters with CORRECTED constraints
9. `Signals_Links` — cluster_event_links, participations with CORRECTED indexes
10. `Advisories_Core` — official_posts, official_post_scopes
11. `Social_Follows` — follows table
12. `Notifications_Core` — notifications table
13. `Civis_Core` — civis_decisions, tda_snapshots
14. `Audit_Core` — admin_audit_logs with all columns including before/after snapshot
15. `Outbox_Core` — outbox_events + state change trigger
16. `Signals_Trigger` — fn_emit_cluster_state_changed trigger

### Critical: constraint corrections vs original DDL

The original `01_postgres_schema.sql` has two broken constraints. Do NOT replicate them:

```csharp
// WRONG — do not use this in your migration:
// unique(locality_id, category, spatial_cell_id, state, title)

// CORRECT — use this partial unique index instead:
migrationBuilder.Sql(@"
    CREATE UNIQUE INDEX ix_clusters_active_identity
    ON signal_clusters(locality_id, category, spatial_cell_id)
    WHERE state IN ('unconfirmed', 'active', 'possible_restoration');
");

// WRONG — do not use this:
// unique(cluster_id, device_id, participation_type, idempotency_key)

// CORRECT — two separate partial indexes:
migrationBuilder.Sql(@"
    CREATE UNIQUE INDEX ix_participations_device_cluster_type
    ON participations(cluster_id, device_id, participation_type)
    WHERE device_id IS NOT NULL;

    CREATE UNIQUE INDEX ix_participations_idempotency
    ON participations(idempotency_key)
    WHERE idempotency_key IS NOT NULL;
");
```

---

## Gate 3 — Auth implementation

### Service interfaces

```csharp
public interface ISmsProvider
{
    Task<bool> SendOtpAsync(string phoneE164, string otpCode, CancellationToken ct);
}

// Africa's Talking implementation
public class AfricasTalkingSmsProvider : ISmsProvider { ... }
```

### OTP generation rules

- OTP length: 6 digits
- OTP TTL: 10 minutes
- Store as bcrypt hash (cost factor 10 minimum)
- Max 3 verification attempts per challenge before invalidation
- Rate limit: 3 OTP requests per destination per 10 minutes (Redis: `rl:otp:{destination}`)

### JWT rules

```csharp
// Claims to include:
// sub = account_id
// account_type = "citizen" | "institution_user" | "admin"
// device_id = device_id (for per-device session invalidation)
// jti = unique token id (for revocation if needed)

// DO NOT include: role, institution_id in JWT claims
// Role and institution membership are always resolved fresh from DB on each request
// This prevents stale permission bugs after role changes
```

### Refresh token rotation

```csharp
// On POST /v1/auth/refresh:
// 1. Look up refresh_tokens by SHA-256 hash of incoming token
// 2. If not found: 401
// 3. If revoked_at is set: TOKEN THEFT DETECTED
//    - Revoke all refresh_tokens for this account immediately
//    - Return 401 with code "token_family_compromised"
//    - Log security event
// 4. If expired: 401
// 5. Generate new access token + new refresh token
// 6. Mark old row: revoked_at = now(), replaced_by_token_hash = new_hash
// 7. Insert new row
// 8. Return new tokens
```

### Rate limiting (Redis)

```
POST /v1/auth/otp          → 3 requests per 10 min per destination
POST /v1/auth/verify       → 3 attempts per challenge (enforced per challengeId; challenge invalidated after 3 failed attempts)
POST /v1/signals/preview   → 20 per min per device
POST /v1/signals/submit    → 5 per 5 min per device
POST /v1/clusters/*/participation → 10 per min per device
```

---

## Gate 4 — Signal preview and NLP

See `docs/arch/09_nlp_integration.md` for the full NLP implementation guide.

### Preview endpoint flow

```
POST /v1/signals/preview
  1. Validate request shape
  2. Rate limit check (Redis key: rl:signal-preview:{deviceHash})
  3. Coarse location resolution (H3 cell from lat/lng if provided)
  4. Call INlpExtractionService.ExtractAsync(text, context)
  5. Validate extraction output against canonical taxonomy
  6. Run similarity search against active/unconfirmed clusters
     - Same category, within H3 ring(3) of spatial_cell_id, last 24h
     - Score each candidate with join_score formula
  7. Return candidates + matching clusters
```

### Join score formula (locked — do not change)

```csharp
public decimal ComputeJoinScore(
    SignalCandidate candidate,
    SignalCluster existingCluster)
{
    decimal categoryScore   = candidate.Category == existingCluster.Category ? 1.0m : 0.0m;
    decimal distanceScore   = ComputeDistanceScore(candidate, existingCluster);  // 0..1
    decimal timeScore       = ComputeTimeScore(candidate, existingCluster);      // 0..1
    decimal conditionScore  = ComputeConditionScore(candidate, existingCluster); // 0..1

    return 0.40m * categoryScore
         + 0.25m * distanceScore
         + 0.20m * timeScore
         + 0.15m * conditionScore;
}
// Join if score >= 0.65 (CIVIS_JOIN_THRESHOLD from config)
```

---

## Gate 5 — Clustering and CIVIS

See `docs/arch/05_civis_engine.md` for the complete CIVIS implementation guide.

### Signal submit flow

```
POST /v1/signals/submit
  1. Validate idempotency key (Redis: idem:signal-submit:{key})
  2. Validate device rate limit
  3. Resolve or create location_label record
  4. Compute H3 spatial_cell_id (resolution 9)
  5. Persist signal_events row
  6. Persist cluster_event_link or create pending cluster
  7. Emit outbox_events row: signal.submitted
  8. Return 201 with cluster reference

Background (worker picks up outbox event):
  civis_assess_worker → evaluates MACF, diversity, activation
  lifecycle_transition_worker → transitions cluster state if gate passed
```

---

## Gate 6 — Participation

### Participation endpoint rules

```csharp
// Before creating participation row:
// 1. Check device is not blocked
// 2. Check cluster exists and is in active or possible_restoration state
// 3. Check idempotency key not already used
// 4. If type == no_longer_affected or restoration_*:
//    - Verify device has a prior "affected" participation on this cluster
// 5. "Add Further Context" (POST /clusters/{id}/context):
//    - Only valid if most recent participation type == affected
//    - Only valid within CIVIS_CONTEXT_EDIT_WINDOW_MINUTES of that participation
// 6. Insert participation row
// 7. Update signal_clusters.affected_count / observing_count atomically
// 8. Emit outbox event: cluster.participation_recorded
```

---

## Gate 7 — Official updates

### Institution endpoint rules

```csharp
// POST /v1/institution/official-updates
// Authorization checks (in order):
// 1. Account has institution_user account_type
// 2. Account has active institution_membership
// 3. Membership role >= institution_operator
// 4. Scopes in request are subsets of institution's institution_jurisdictions
//    (ST_Contains or ST_Intersects check for geom scopes)
// 5. Category in request is within institution's allowed categories

// If is_restoration_claim == true AND related_cluster_id is set:
//   - Emit outbox: official_post.restoration_claim
//   - lifecycle_transition_worker handles possible_restoration transition
```

---

## Gate 8 — Restoration lifecycle

See `docs/arch/05_civis_engine.md` for restoration confirmation rules.

### Restoration flow

```
Institution posts is_restoration_claim=true linked to clusterId
  OR
Citizen taps "No Longer Affected" (no_longer_affected participation)
  → Either event can trigger possible_restoration if threshold rules met
  → lifecycle_transition_worker evaluates:
       restoration_ratio = weighted_restoration_yes / (yes + still_affected_count)
       if ratio >= 0.60 AND distinct_affected_devices >= 2
       → cluster state = resolved

Affected cohort push notification:
  → "Is service restored at [location]? Let us know."
  → Deep link: hali://clusters/{id}/restoration
```

---

## Structured logging requirements

Every significant action must log a structured event. Use Serilog with these property names:

```csharp
Log.Information("Signal submitted {@Context}", new {
    EventType = "signal.submitted",
    SignalEventId = signalEvent.Id,
    ClusterId = cluster.Id,
    ClusterState = cluster.State,
    Category = signalEvent.Category,
    LocalityId = signalEvent.LocalityId,
    Joined = joined,
    DeviceId = device.Id,
    CorrelationId = correlationId
});
```

**Required log events:**
- `auth.otp_requested`, `auth.otp_verified`, `auth.token_refreshed`, `auth.token_family_compromised`
- `signal.submitted`, `signal.preview_generated`
- `cluster.created`, `cluster.activated`, `cluster.state_changed`
- `participation.recorded`
- `official_update.published`, `official_update.restoration_claim`
- `civis.assessment_completed`, `civis.activation_gate_passed`, `civis.activation_gate_failed`
- `worker.processing_error`, `worker.dlq_message`

---

## Tests required for Phase 1 gate

```
Unit tests (must pass):
  - CivisEngine: MACF calculation for each category
  - CivisEngine: decay kernel for each category's half-life
  - CivisEngine: join score formula
  - CivisEngine: restoration ratio threshold
  - CivisEngine: device diversity gate (≥2 unique devices required)
  - ClusterLifecycle: unconfirmed → active transition
  - ClusterLifecycle: active → possible_restoration transition
  - ClusterLifecycle: possible_restoration → resolved transition
  - ClusterLifecycle: possible_restoration → active revert
  - Auth: refresh token rotation, theft detection
  - Participation: idempotency enforcement
  - Participation: context edit window enforcement

Integration tests (must pass):
  - Full signal preview → submit → cluster creation flow
  - Signal submitted to existing cluster → join flow
  - OTP request → verify → access token → refresh → logout flow
  - Official update published → appears in home feed for locality
  - Restoration claim → possible_restoration → citizen response → resolved
```
