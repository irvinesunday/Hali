import { InstitutionColors, InstitutionTheme, SharedSemanticColors } from "./colors";

// Maps a camelCase token key to its CSS custom-property name.
// `cardForeground` → `--color-card-foreground`; `sidebarPrimaryForeground`
// → `--color-sidebar-primary-foreground`.
function cssVarName(key: string): `--color-${string}` {
  const kebab = key.replace(/([A-Z])/g, "-$1").toLowerCase();
  return `--color-${kebab}`;
}

function buildVars(theme: Record<string, string>): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(theme)) {
    out[cssVarName(key)] = value;
  }
  return out;
}

/**
 * CSS custom-property declarations for the institution surface. Drives
 * Tailwind's `colors.*` resolution in downstream web apps so token
 * values are sourced from this package rather than duplicated in app
 * stylesheets. Mirror for citizen-mobile is the React Native theme
 * object — do not import this module from the mobile app.
 */
export const institutionCssVariables: Readonly<Record<string, string>> = Object.freeze({
  ...buildVars(SharedSemanticColors as unknown as Record<string, string>),
  ...buildVars(InstitutionColors as unknown as Record<string, string>),
});

// Re-expose for test assertions that want to confirm the expanded set
// matches the flat `InstitutionTheme` composition.
export type InstitutionThemeKey = keyof typeof InstitutionTheme;
