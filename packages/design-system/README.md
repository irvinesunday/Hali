# @hali/design-system

Shared Hali design system tokens and primitives.

**Web-only.** Never import this package from `apps/citizen-mobile`.
Mobile ships its own React Native theme in
`apps/citizen-mobile/src/theme/` because React Native style primitives
and web CSS are not interchangeable. Keeping them separate is a
deliberate boundary — see `docs/arch/FEATURE_FLIGHTING_MODEL.md` and
`CLAUDE.md` stack rules.

## Usage

```ts
import { DesignSystemVersion } from "@hali/design-system";
import { /* tokens */ } from "@hali/design-system/tokens";
```

The concrete token set — colors, typography, spacing, radius, shadows,
and the shell / dashboard primitives — is populated by the
design-system extraction work. See that PR for the canonical source
and the v0 visual audit.
