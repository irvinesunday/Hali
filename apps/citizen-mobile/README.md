# Hali Citizen Mobile

React Native (Expo) citizen app for the Hali MVP.

## Stack

| Concern | Decision |
|---|---|
| Framework | React Native + Expo (managed workflow) |
| Language | TypeScript (strict mode) |
| Navigation | Expo Router (file-based) |
| State | React Context + useReducer for auth; TanStack Query for server state |
| HTTP | Axios with 401 → refresh token interceptor |
| Storage | expo-secure-store (`HALI_ACCESS_TOKEN`, `HALI_REFRESH_TOKEN`, `HALI_ACCOUNT_ID`) |
| Push | expo-notifications (Expo Push Tokens) |
| Forms | react-hook-form |
| Icons | @expo/vector-icons |

## Dev Setup

### Prerequisites

- Node.js 20+
- npm or yarn
- Expo CLI: `npm install -g expo-cli` (or use `npx expo`)
- Expo Go app on your device, or iOS Simulator / Android Emulator

### 1. Install dependencies

```bash
cd apps/citizen-mobile
npm install
```

### 2. Configure environment

```bash
cp .env.example .env
```

Edit `.env` and set `EXPO_PUBLIC_API_BASE_URL` to point at the running backend:

```
EXPO_PUBLIC_API_BASE_URL=http://192.168.x.x:8080   # LAN IP for device testing
```

### 3. Start the backend

From the repo root:

```bash
docker compose -f 07_docker-compose.yml up -d
dotnet run --project src/Hali.Api
```

### 4. Start Expo

```bash
npx expo start
```

Scan the QR code with Expo Go, or press `i` for iOS simulator / `a` for Android emulator.

---

## Screen Inventory

| Screen | Route |
|---|---|
| Splash / Boot | `/` |
| Phone Entry | `/(auth)/phone` |
| OTP Verification | `/(auth)/otp` |
| Home Feed | `/(app)/home` |
| Cluster Detail | `/(app)/clusters/[id]` |
| Signal Composer Step 1 | `/(app)/compose/text` |
| Signal Composer Step 2 | `/(app)/compose/confirm` |
| Signal Composer Step 3 | `/(app)/compose/submit` |
| Restoration Prompt | `/(app)/clusters/[id]/restore` |
| Ward Following Settings | `/(app)/settings/wards` |
| Notification Settings | `/(app)/settings/notifications` |
| Account Settings | `/(app)/settings/account` |

## Auth Flow

```
Splash → check SecureStore → authenticated? → Home Feed
                           → unauthenticated? → Phone Entry → OTP → Home Feed
```

Token refresh happens transparently via Axios interceptor on 401 responses.

## Known API Notes

- `POST /v1/auth/verify` — actual backend route (OpenAPI spec has `/verify-otp`, spec is stale)
- `POST /v1/signals/submit` — actual backend route (OpenAPI spec has `/v1/signals`)
- `POST /v1/clusters/{id}/participation` — singular (OpenAPI spec has `participations`)
- `shouldSuggestJoin` (bool) in preview response — backend does not return `existingClusterCandidates[]` as specified in OpenAPI; the composer UI uses the bool flag instead

## Folder Structure

```
apps/citizen-mobile/
  src/
    api/           — typed service modules (no `any`)
    components/    — clusters/, signals/, common/
    context/       — AuthContext, LocalityContext, ComposerContext
    hooks/         — useAuth, useClusters, useSignalComposer
    config/        — env.ts, constants.ts
    types/         — api.ts, domain.ts
    utils/         — formatters.ts, validators.ts
  app/             — Expo Router pages
    (auth)/        — phone.tsx, otp.tsx
    (app)/         — home.tsx, clusters/, compose/, settings/
    _layout.tsx    — root providers
    index.tsx      — splash/boot
```
