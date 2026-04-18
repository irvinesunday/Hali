import { institutionCssVariables } from "@hali/design-system";

// Applies the institution surface's CSS custom properties to the given
// root element. Called once at application boot before React renders,
// so Tailwind's var(--color-*) resolutions (bg-background, etc.) have
// values before the first paint.
export function applyInstitutionTokens(root: HTMLElement = document.documentElement): void {
  for (const [name, value] of Object.entries(institutionCssVariables)) {
    root.style.setProperty(name, value);
  }
}
