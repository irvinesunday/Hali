# @hali/config

Non-secret app constants shared across Hali surfaces — civic category
enums, signal-state enums, and wire-level limits (composer character
cap, max wards followed, etc.).

## Usage

```ts
import { CIVIC_CATEGORIES, SIGNAL_TEXT_MAX_LENGTH } from "@hali/config";
```

## Scope

- ✅ Enum values that are defined authoritatively on the backend and
  must stay in sync on the client.
- ✅ Wire-level numeric limits that are validated on both sides.
- ❌ Secrets, API keys, environment-dependent URLs — see
  `docs/arch/SECURITY_POSTURE.md` §5.
- ❌ Product strings / copy — those live per surface (mobile / web
  have different copy constraints).
