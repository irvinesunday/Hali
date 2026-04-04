# Hali — API Contracts Reference
**All routes, request/response shapes, and rules across all phases.**

This file is the companion to `02_openapi.yaml`. When there is a conflict, this file wins for route naming and shapes. Update the OpenAPI YAML to match.

---

## API design rules

- All routes versioned under `/v1/`
- All timestamps ISO 8601 UTC
- All mutation endpoints use `idempotencyKey` in the request body for idempotency; request bodies must not use a separate header
- Cursor-based pagination on all list endpoints
- Public responses never expose CIVIS internals, device data, or account PII
- Every write returns the updated object and its stable ID

### Standard pagination

```
Query params: ?cursor={opaque}&limit={int, default 25, max 100}

Response envelope:
{
  "items": [...],
  "pagination": {
    "nextCursor": "string | null",
    "hasMore": true,
    "totalCount": 142    // best-effort estimate
  }
}
```

### Standard error model

| HTTP | Code | Meaning |
|---|---|---|
| 400 | `invalid_request` | Validation or shape issue |
| 401 | `unauthorized` | Auth required or expired |
| 403 | `forbidden_scope` | Role or scope check failed |
| 404 | `not_found` | Object missing |
| 409 | `idempotency_conflict` | Same key, different payload |
| 422 | `policy_blocked` | Violates product or trust policy |
| 429 | `rate_limited` | Burst limit exceeded |

```json
{
  "error": {
    "code": "policy_blocked",
    "message": "Maximum followed wards reached.",
    "detail": { "limit": 5, "current": 5 }
  }
}
```

---

## Phase 1 — Citizen routes

### Auth

**POST /v1/auth/otp**
```json
// Request
{
  "method": "phone_otp | email_otp | magic_link",
  "destination": "+254712345678 | user@example.com"
}
// Response 202
{ "challengeId": "uuid", "expiresAt": "2026-04-03T14:40:00Z" }
```

**POST /v1/auth/verify**
```json
// Request
{
  "challengeId": "uuid",
  "otpCode": "123456",
  "deviceFingerprint": "string"
}
// Response 200
{
  "accessToken": "jwt",
  "refreshToken": "opaque-random-string",
  "expiresIn": 3600,
  "account": {
    "id": "uuid",
    "accountType": "citizen",
    "displayName": "string | null"
  }
}
```

**POST /v1/auth/refresh**
```json
// Request
{ "refreshToken": "opaque-string" }  // body for mobile; cookie for web (see auth guide)
// Response 200
{
  "accessToken": "jwt",
  "refreshToken": "new-opaque-string",  // rotated
  "expiresIn": 3600
}
// Error 401 if token expired or revoked
```

**POST /v1/auth/logout**
```json
// Request: no body required (token in Authorization header or cookie)
// Response 204
// Backend: revoke refresh token associated with current device
```

---

### Home feed

**GET /v1/home**
```
Query: ?localityId={uuid}  (optional if user has followed wards)

Response 200:
{
  "localityName": "South B, Nairobi",
  "isCalmState": false,
  "lastCheckedAt": "2026-04-03T14:30:00Z",
  "sections": {
    "activeNow": [ClusterSummaryDto],
    "officialUpdates": [OfficialUpdateSummaryDto],
    "recurringAtThisTime": [ClusterSummaryDto],
    "otherActiveSignals": [ClusterSummaryDto]
  }
}
```

**ClusterSummaryDto** (citizen-safe — no CIVIS internals):
```json
{
  "id": "uuid",
  "category": "electricity",
  "subcategorySlug": "outage",
  "dominantConditionSlug": "no_power",
  "state": "active",
  "temporalType": "continuous",
  "title": "Power Outage",
  "summary": "No electricity in South B since morning.",
  "locationLabel": "South B, Nairobi West",
  "publicConfirmationCount": 18,
  "affectedCount": 12,
  "observingCount": 6,
  "firstSeenAt": "2026-04-03T08:15:00Z",
  "lastSeenAt": "2026-04-03T14:20:00Z",
  "restorationState": "none | possible | confirmed"
}
```

**OfficialUpdateSummaryDto**:
```json
{
  "id": "uuid",
  "institutionName": "Kenya Power",
  "officialPostType": "live_update",
  "category": "electricity",
  "title": "Teams dispatched to South B",
  "publishedAt": "2026-04-03T10:30:00Z",
  "startsAt": null,
  "endsAt": null,
  "relatedClusterId": "uuid | null"
}
```

---

### Signals

**POST /v1/signals/preview**
```json
// Request
{
  "text": "Big potholes near National Oil in Nairobi West. Very bad after the rain.",
  "sourceLanguage": "en",
  "location": {
    "latitude": -1.302,
    "longitude": 36.814,
    "userEnteredPlace": "Nairobi West"
  }
}
// Response 200
{
  "candidates": [
    {
      "category": "roads",
      "subcategorySlug": "potholes",
      "conditionSlug": "difficult",
      "conditionConfidence": 0.85,
      "neutralSummary": "Potholes affecting road near National Oil, Nairobi West.",
      "locationLabel": "Lusaka Road near National Oil, Nairobi West",
      "locationConfidence": 0.82,
      "locationPrecisionType": "landmark",
      "temporalType": "episodic_unknown",
      "temporalConfidence": 0.64,
      "shouldSuggestJoin": true
    }
  ],
  "existingClusterCandidates": [
    {
      "clusterId": "uuid",
      "summary": "Potholes affecting road in Nairobi West",
      "locationLabel": "Lusaka Road, Nairobi West",
      "publicConfirmationCount": 9,
      "state": "active"
    }
  ]
}
```

**POST /v1/signals/submit**
```json
// Request
{
  "candidate": { /* SignalCandidate — confirmed by user */ },
  "joinClusterId": "uuid | null",
  "deviceFingerprint": "string",
  "idempotencyKey": "client-generated-uuid"
}
// Response 201
{
  "signalEventId": "uuid",
  "clusterId": "uuid",
  "clusterState": "unconfirmed | active",
  "joined": true
}
```

---

### Clusters

**GET /v1/clusters/{id}**
```json
// Response 200 — full cluster detail, citizen-safe
{
  "cluster": { /* ClusterDetailDto — extends ClusterSummaryDto */ },
  "officialUpdates": [OfficialUpdateSummaryDto],  // linked updates, newest first
  "userParticipation": {
    "type": "affected | observing | no_longer_affected | null",
    "canAddContext": true,
    "contextEditWindowExpiresAt": "2026-04-03T14:32:00Z | null"
  },
  "restorationProgress": {
    "state": "none | possible | confirmed",
    "possibleRestorationAt": "2026-04-03T13:00:00Z | null",
    "restorationRatio": 0.72,      // only shown when state = possible
    "threshold": 0.60
  }
}
```

**POST /v1/clusters/{id}/participation**
```json
// Request
{
  "type": "affected | observing | no_longer_affected | restoration_yes | restoration_no | restoration_unsure",
  "contextText": "string max 150 chars, nullable",
  "idempotencyKey": "client-generated-uuid"
}
// Response 202
{
  "participationId": "uuid",
  "clusterState": "active | possible_restoration"
}
```

**POST /v1/clusters/{id}/context**
```json
// Request — only valid after "affected" participation within context edit window
{
  "contextText": "string max 150 chars",
  "idempotencyKey": "client-generated-uuid"
}
// Response 202 — or 422 policy_blocked if window expired
```

**POST /v1/clusters/{id}/restoration-response**
```json
// Request
{
  "response": "restoration_yes | restoration_no | restoration_unsure",
  "idempotencyKey": "client-generated-uuid"
}
// Response 202
{ "clusterState": "possible_restoration | active | resolved" }
```

---

### Localities and follows

**GET /v1/localities/followed**
```json
// Response 200
{
  "followed": [
    { "localityId": "uuid", "wardName": "South B", "cityName": "Nairobi", "followedAt": "..." }
  ],
  "count": 3,
  "maxAllowed": 5
}
```

**PUT /v1/localities/followed**
```json
// Request — full replacement of followed set
{
  "localityIds": ["uuid", "uuid"]  // max 5; 422 policy_blocked if >5
}
// Response 200 — returns updated followed list
```

---

### Users and devices

**GET /v1/users/me**
```json
// Response 200
{
  "id": "uuid",
  "accountType": "citizen",
  "displayName": "string | null",
  "isPhoneVerified": true,
  "notificationSettings": {
    "pushEnabled": true,
    "restorationPrompts": true,
    "newClustersInFollowedWards": true
  }
}
```

**PUT /v1/users/me/notification-settings**
```json
// Request
{
  "pushEnabled": true,
  "restorationPrompts": true,
  "newClustersInFollowedWards": false
}
// Response 200 — updated settings
```

**POST /v1/devices/push-token**
```json
// Request
{
  "pushToken": "ExponentPushToken[...]",
  "platform": "ios | android"
}
// Response 200
{ "registered": true }
```

---

## Phase 2 — Institution Operations routes

All `/v1/institution/*` routes require `institution_operator` role minimum. Responses are filtered to the acting user's institution and geo/category scopes server-side.

**GET /v1/institution/overview**
```json
{
  "institutionId": "uuid",
  "institutionName": "Kenya Power",
  "scopeSummary": { "wardCount": 12, "categories": ["electricity"] },
  "activeClustersInScope": 4,
  "possibleRestorationPending": 2,
  "unacknowledgedClusters": 1,
  "recentOfficialUpdates": [OfficialUpdateSummaryDto],
  "trendCards": [
    { "category": "electricity", "activeLast24h": 7, "resolvedLast24h": 3 }
  ],
  "freshnessTimestamp": "2026-04-03T14:30:00Z"
}
```

**GET /v1/institution/clusters**
```
Query: ?localityId=&category=&state=&restorationState=&dateFrom=&dateTo=&cursor=&limit=
```
Returns paginated `InstitutionClusterSummaryDto` — no CIVIS fields, no PII.

**POST /v1/institution/official-updates**
```json
// Request
{
  "officialPostType": "live_update | scheduled_disruption | advisory_public_notice",
  "category": "electricity",
  "title": "Teams dispatched to South B",
  "body": "Kenya Power engineers are on site investigating the outage.",
  "startsAt": null,
  "endsAt": null,
  "relatedClusterId": "uuid | null",
  "isRestorationClaim": false,
  "scopes": [{ "localityId": "uuid" }]
}
// Response 201
{ "id": "uuid", "status": "published", "createdAt": "..." }
```

**GET /v1/institution/restoration**
```json
{
  "items": [
    {
      "clusterId": "uuid",
      "clusterSummary": "...",
      "locationLabel": "...",
      "possibleRestorationAt": "...",
      "restorationRatio": 0.42,
      "affectedVoteCount": 5,
      "totalAffectedCount": 12,
      "threshold": 0.60
    }
  ],
  "pagination": { ... }
}
```

---

## Phase 2 — Institution Admin routes

All `/v1/institution-admin/*` routes require `institution_admin` role. Always bounded to acting user's institution.

- `GET /v1/institution-admin/users` — list institution users
- `POST /v1/institution-admin/users` — invite user
- `PATCH /v1/institution-admin/users/{id}` — update role or active status
- `GET /v1/institution-admin/roles` — view role/permission matrix
- `GET|POST|DELETE /v1/institution-admin/scopes` — manage geo scopes
- `GET|POST|DELETE /v1/institution-admin/category-scopes` — manage category scopes
- `GET|POST|PATCH /v1/institution-admin/templates` — official update templates
- `GET|POST /v1/institution-admin/notification-recipients` — alert recipients
- `GET /v1/institution-admin/audit` — institution-scoped audit log
- `GET|PATCH /v1/institution-admin/settings` — institution metadata and defaults

---

## Phase 3 — Hali Ops routes

All `/v1/ops/*` routes require Hali ops role. Role segmentation within ops is:

| Route | Minimum role |
|---|---|
| `/v1/ops/overview` | Any ops role |
| `/v1/ops/localities` | operations_admin |
| `/v1/ops/institutions` | institution_onboarding_admin or operations_admin |
| `/v1/ops/signals`, `/v1/ops/clusters` | operations_admin |
| `/v1/ops/integrity` | trust_integrity_analyst |
| `/v1/ops/orphaned-signals` | trust_integrity_analyst |
| `/v1/ops/taxonomy` | operations_admin |
| `/v1/ops/audit` | super_admin |
| `/v1/ops/settings/civis` | super_admin only |

**PATCH /v1/ops/settings/civis**
```json
// Request — partial update of CIVIS constants
{
  "roads": { "baseFloor": 2, "halfLifeHours": 18, "macfMin": 2, "macfMax": 6 },
  "water": { "halfLifeHours": 24 }
}
// Response 200
{
  "ruleVersion": "2026.04.03.001",
  "updatedAt": "...",
  "updatedBy": "account-uuid",
  "snapshot": { /* full CIVIS constants after update */ }
}
// Side effect: writes admin_audit_log with before_snapshot, after_snapshot, rule_version
```

**GET /v1/ops/clusters/{id}/civis-history**
```json
{
  "clusterId": "uuid",
  "decisions": [
    {
      "id": "uuid",
      "decisionType": "activation_evaluation",
      "reasonCodes": ["low_diversity", "macf_not_met"],
      "metrics": { "wrab": 2.1, "sds": 1.4, "macf": 3, "uniqueDeviceCount": 1 },
      "createdAt": "..."
    }
  ]
}
```

**GET /v1/ops/orphaned-signals**
```
Query: ?localityId=&category=&cursor=&limit=
Returns: Clusters in active/possible_restoration state with no institution jurisdiction match.
Ranked by: recurrence_confidence DESC, public_confirmation_count DESC.
```
