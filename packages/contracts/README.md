# @hali/contracts

Framework-agnostic TypeScript types derived from the Hali OpenAPI
contract (`02_openapi.yaml`). Consumed by every front-end surface so the
wire contract is shared and drift is caught at the type level.

## Usage

```ts
import type { ResolvedFeatureFlagsResponse } from "@hali/contracts";
```

## Contract discipline

- Every type in this package corresponds to a wire shape defined in
  `02_openapi.yaml`. Backend-internal types do not belong here.
- When an OpenAPI shape changes, update the type in this package in the
  same PR.
- Never export types that only one surface uses — those live with that
  surface.
