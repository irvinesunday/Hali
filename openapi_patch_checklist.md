# OpenAPI Patch Checklist

Update the OpenAPI spec so it is the sole route authority.

## Step 1 — Remove these existing paths entirely
The following paths exist in the current 02_openapi.yaml and must be deleted.
Do not leave them alongside the new paths.

- `POST /v1/auth/request-otp`  → replaced by `POST /v1/auth/otp`
- `POST /v1/auth/verify-otp`   → replaced by `POST /v1/auth/verify`
- `POST /v1/clusters/{clusterId}/participations`  → replaced by `POST /v1/clusters/{id}/participation` (singular, id not clusterId)
- `POST /v1/signals`           → replaced by `POST /v1/signals/submit`
- `POST /v1/localities/followed` → replaced by `PUT /v1/localities/followed` (method change, not just rename)
- `GET /v1/clusters`           → keep, but confirm query params match schema
- `/v1/official-posts`         → out of scope for citizen MVP client; keep in spec but tag as InstitutionOnly
- `/v1/admin/orphaned-signals` → out of scope for citizen MVP client; keep in spec but tag as AdminOnly

## Step 2 — Add these required paths
All paths must use `/v1/*` versioning. All mutations accept `Idempotency-Key` header.

- `POST /v1/auth/otp`
- `POST /v1/auth/verify`          — response must include access_token + refresh_token
- `POST /v1/auth/refresh`         — response must include new access_token (+ rotated refresh_token if rotation implemented)
- `POST /v1/auth/logout`
- `GET  /v1/home`                 — see response schema below
- `GET  /v1/users/me`
- `POST /v1/signals/preview`      — already exists, keep
- `POST /v1/signals/submit`
- `GET  /v1/clusters/{id}`
- `POST /v1/clusters/{id}/participation`
- `POST /v1/clusters/{id}/context`
- `POST /v1/clusters/{id}/restoration-response`
- `GET  /v1/localities/followed`  — already exists, keep
- `PUT  /v1/localities/followed`  — replaces POST; PUT semantics = replace full followed set
- `POST /v1/devices/push-token`
- `PUT  /v1/users/me/notification-settings`

## Step 3 — Update existing component schemas

### SignalCandidate — add missing NLP output fields
The existing SignalCandidate schema is missing three fields that the NLP layer outputs
and the Step 2 confirmation UI requires. Add these properties:

    conditionConfidence: { type: number, minimum: 0, maximum: 1, nullable: true }
    locationSource: { type: string, enum: [nlp, search, pin], nullable: true }
    locationPrecisionType: { type: string, nullable: true }

### HomeFeedResponse — define inline (no external $ref)
Add as a new component schema. Use inline object shapes — do not reference
ClusterSummary, OfficialPostSummary, or LocalitySummary as they are not defined:

    HomeFeedResponse:
      type: object
      properties:
        activeNow:
          type: array
          items:
            type: object
            properties:
              id: { type: string, format: uuid }
              category: { type: string }
              subcategorySlug: { type: string }
              state: { type: string }
              temporalType: { type: string }
              title: { type: string }
              summary: { type: string }
              locationLabel: { type: string }
              rawConfirmationCount: { type: integer }
              affectedCount: { type: integer }
              observingCount: { type: integer }
              lastSeenAt: { type: string, format: date-time }
        officialUpdates:
          type: array
          items:
            type: object
            properties:
              id: { type: string, format: uuid }
              officialPostType: { type: string }
              category: { type: string, nullable: true }
              title: { type: string }
              body: { type: string }
              startsAt: { type: string, format: date-time, nullable: true }
              endsAt: { type: string, format: date-time, nullable: true }
        recurringAtThisTime:
          type: array
          items: { $ref: '#/components/schemas/HomeFeedClusterItem' }
        otherActiveSignals:
          type: array
          items: { $ref: '#/components/schemas/HomeFeedClusterItem' }
        followedLocalityIds:
          type: array
          items: { type: string, format: uuid }

    HomeFeedClusterItem:
      type: object
      properties:
        id: { type: string, format: uuid }
        category: { type: string }
        state: { type: string }
        temporalType: { type: string }
        title: { type: string }
        summary: { type: string }
        locationLabel: { type: string }
        rawConfirmationCount: { type: integer }
        lastSeenAt: { type: string, format: date-time }

## Step 4 — Required response schema: GET /v1/home
Attach HomeFeedResponse as the 200 response schema for GET /v1/home.

## Token responses
- OTP verification must return access_token + refresh_token
- Refresh endpoint must return new access_token and rotated refresh_token
