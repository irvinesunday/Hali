# Hali

> Civic Signal Infrastructure — a civic weather radar for your city.

Citizens report conditions in natural language. Hali extracts structured civic signals,
clusters related reports spatially and temporally, and activates public clusters when
evidence meets the CIVIS confidence threshold. Institutions respond with official updates.
Citizens confirm restoration. The loop closes.

---

## What's Built

| Layer | Stack | Status |
|---|---|---|
| Backend API | .NET / C# modular monolith | Production-ready |
| Database | PostgreSQL 16 + PostGIS 3.4 | Migrations applied |
| Cache / queues | Redis 7 | Connected |
| Mobile app | React Native + Expo (TypeScript) | 12 screens spec'd |
| NLP extraction | Anthropic Claude Sonnet (CSI-NLP) | Integrated |
| Auth | Phone OTP + JWT + refresh tokens | Complete |
| Institution auth | Invite-based onboarding (B-5) | Complete |
| Spatial indexing | H3 resolution 9 | Integrated |
| SMS / OTP | Africa's Talking | Integrated |
| Push notifications | Expo Push API | Integrated |
| Geocoding | Nominatim / OpenStreetMap | Integrated |

---

## Test Coverage

- **Unit tests:** 139 passing
- **Integration tests:** 1 passing (Testcontainers)
- NLP, SMS, and push notification services are stubbed in all tests

---

## Running Locally

```bash
# Backend
cp .env.example .env
docker compose -f 07_docker-compose.yml up -d
dotnet ef database update --project src/Hali.Infrastructure --startup-project src/Hali.Api
dotnet run --project src/Hali.Api

# Mobile
cd apps/citizen-mobile
cp .env.example .env
npx expo start
```

---

## Repository Structure

```
src/
  Hali.Api/           -- REST API host + Dockerfile
  Hali.Application/   -- Use cases, services, interfaces
  Hali.Domain/        -- Entities, enums, domain logic
  Hali.Infrastructure/-- DB contexts, repositories, external adapters
  Hali.Workers/       -- Background jobs (outbox relay, decay)
  Hali.Contracts/     -- Shared request/response DTOs
tests/
  Hali.Tests.Unit/    -- Pure unit tests (no DB, no network)
  Hali.Tests.Integration/ -- Integration tests (Testcontainers)
apps/
  citizen-mobile/     -- React Native + Expo citizen app
docs/
  runbooks/           -- Operational procedures
  staging-env-guide.md
```

---

## Architecture

See the following key documents:
- `CLAUDE.md` — engineering doctrine and implementation authority
- `mvp_locked_decisions.md` — all locked product and technical decisions
- `02_openapi.yaml` — API contract (source of truth for all endpoint paths)
- `mobile_screen_inventory.md` — mobile screen specs and API dependencies
- `HANDOVER.md` — complete build guide and multi-agent setup

---

## API Overview

All routes are versioned under `/v1/`. See `02_openapi.yaml` for full schemas.

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| POST | `/v1/auth/otp` | - | Request OTP |
| POST | `/v1/auth/verify` | - | Verify OTP, issue tokens |
| POST | `/v1/auth/refresh` | - | Refresh token pair |
| POST | `/v1/auth/logout` | - | Revoke refresh token |
| POST | `/v1/auth/institution/setup` | - | Accept invite, register phone |
| GET | `/v1/home` | optional | Paginated home feed |
| POST | `/v1/signals/preview` | bearer | NLP extraction preview |
| POST | `/v1/signals/submit` | bearer | Submit signal event |
| GET | `/v1/clusters/{id}` | - | Cluster detail |
| POST | `/v1/clusters/{id}/participation` | bearer | Affected / Observing |
| POST | `/v1/clusters/{id}/context` | bearer | Add further context |
| POST | `/v1/clusters/{id}/restoration-response` | bearer | Restoration vote |
| GET | `/v1/localities/followed` | bearer | List followed wards |
| PUT | `/v1/localities/followed` | bearer | Set followed wards |
| POST | `/v1/official-posts` | institution | Create official post |
| GET | `/v1/users/me` | bearer | User profile |
| PUT | `/v1/users/me/notification-settings` | bearer | Update prefs |
| POST | `/v1/devices/push-token` | bearer | Register push token |
| POST | `/v1/admin/institutions` | admin | Create institution + invite |
| DELETE | `/v1/admin/institutions/{id}/access` | admin | Revoke institution access |

---

## Licence

Private — all rights reserved.
