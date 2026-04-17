// @hali/design-system — shared Hali design system baseline.
//
// WEB-ONLY. Never import this package from apps/citizen-mobile —
// the mobile app maintains its own React Native theme in
// apps/citizen-mobile/src/theme/ because React Native styles and web
// CSS are not interchangeable.
//
// The baseline tokens (colors, typography, spacing, radius, shadows)
// are populated in the design-system extraction work (#189). This
// file currently exposes only the package shape so downstream web
// apps (institution-web, institution-admin-web, future hali-ops-web)
// can import it before the token set lands.

export * from "./tokens";
