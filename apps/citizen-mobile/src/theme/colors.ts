/**
 * Hali design token — colour system.
 *
 * Source: MVP demo globals.css (OKLCH values converted to hex).
 * Primary brand colour is a muted civic teal — calm and trustworthy,
 * not urgent or alarming. All condition badge colours match Tailwind
 * semantic classes used in the web MVP.
 *
 * Do not change these values without updating the web MVP globals.css
 * to keep the two surfaces visually consistent.
 */

export const Colors = {
  // ── Brand ──────────────────────────────────────────────────────────────
  primary: '#2D8A8F',        // oklch(0.55 0.12 190) — civic teal
  primaryForeground: '#FFFFFF',
  primarySubtle: '#E8F4F5',  // primary/10 equivalent

  // ── Surface ────────────────────────────────────────────────────────────
  background: '#F7FAFA',     // oklch(0.98 0.005 200) — near-white, cool tint
  card: '#FFFFFF',
  cardForeground: '#383D42', // oklch(0.25 0.02 220)

  // ── Text ───────────────────────────────────────────────────────────────
  foreground: '#383D42',     // primary text
  mutedForeground: '#757B82',// oklch(0.50 0.02 220) — secondary text
  faintForeground: '#9CA3AF',// very subtle labels, timestamps

  // ── UI Chrome ──────────────────────────────────────────────────────────
  border: '#E2E7E7',         // oklch(0.90 0.01 200)
  muted: '#EEF1F1',          // oklch(0.95 0.005 200) — section backgrounds
  input: '#E8EDED',          // oklch(0.92 0.01 200)

  // ── Semantic ───────────────────────────────────────────────────────────
  destructive: '#D4603A',    // oklch(0.60 0.15 30)
  emerald: '#059669',        // restoration / calm / positive
  emeraldSubtle: '#ECFDF5',

  // ── Condition badge colours ────────────────────────────────────────────
  // Each entry: { bg, text, border }
  // Used by ConditionBadge component — must map every condition slug
  // that CSI-NLP can return. Add new slugs here as taxonomy expands.
  conditionBadge: {
    // Power / water outages — amber
    amber: {
      bg: '#FFFBEB',
      text: '#B45309',
      border: '#FDE68A',
    },
    // Road difficulty — orange
    orange: {
      bg: '#FFF7ED',
      text: '#C2410C',
      border: '#FED7AA',
    },
    // Impassable / blocked — red
    red: {
      bg: '#FEF2F2',
      text: '#B91C1C',
      border: '#FECACA',
    },
    // Flooding / water on road — sky
    sky: {
      bg: '#F0F9FF',
      text: '#0369A1',
      border: '#BAE6FD',
    },
    // Noise disturbance — violet
    violet: {
      bg: '#F5F3FF',
      text: '#6D28D9',
      border: '#DDD6FE',
    },
    // Dust — stone
    stone: {
      bg: '#FAFAF9',
      text: '#57534E',
      border: '#D6D3D1',
    },
    // Dark / no lighting — slate
    slate: {
      bg: '#F8FAFC',
      text: '#475569',
      border: '#CBD5E1',
    },
    // Yellow — slow moving, minor conditions
    yellow: {
      bg: '#FEFCE8',
      text: '#A16207',
      border: '#FEF08A',
    },
    // Restoration / possible recovery — emerald
    emerald: {
      bg: '#ECFDF5',
      text: '#047857',
      border: '#A7F3D0',
    },
    // Fallback
    muted: {
      bg: '#EEF1F1',
      text: '#757B82',
      border: '#E2E7E7',
    },
  },
} as const;

/**
 * Maps a condition label string to a conditionBadge palette key.
 * Extend this map as new conditions are added to the taxonomy.
 *
 * Returns 'muted' for any unrecognised condition — never throws.
 */
export function getConditionBadgePalette(
  condition: string,
): keyof typeof Colors.conditionBadge {
  const c = condition.toLowerCase();

  if (
    c.includes('no power') || c.includes('power outage') ||
    c.includes('blackout') || c.includes('no water') ||
    c.includes('transformer') || c.includes('sewage') ||
    c.includes('bad smell') || c.includes('sewage smell')
  ) return 'amber';

  if (
    c.includes('difficult') || c.includes('pothole') ||
    c.includes('lane blocked') || c.includes('slow') ||
    c.includes('heavy traffic') || c.includes('road damage') ||
    c.includes('partially blocked') || c.includes('road narrowed') ||
    c.includes('obstruction') || c.includes('sidewalk') ||
    c.includes('walkway') || c.includes('crowded') ||
    c.includes('uncollected') || c.includes('dumped') ||
    c.includes('overflowing')
  ) return 'orange';

  if (
    c.includes('impassable') || c.includes('road blocked') ||
    c.includes('traffic blocked') || c.includes('accident') ||
    c.includes('crash') || c.includes('collision')
  ) return 'red';

  if (
    c.includes('flood') || c.includes('water on road') ||
    c.includes('partially flooded') || c.includes('water on')
  ) return 'sky';

  if (
    c.includes('noise') || c.includes('noisy') || c.includes('loud')
  ) return 'violet';

  if (
    c.includes('dust') || c.includes('dusty')
  ) return 'stone';

  if (
    c.includes('dark') || c.includes('no street') || c.includes('no light')
  ) return 'slate';

  if (
    c.includes('low pressure') || c.includes('weak pressure') ||
    c.includes('unstable') || c.includes('intermittent') ||
    c.includes('going on') || c.includes('air pollution')
  ) return 'yellow';

  if (
    c.includes('restoration') || c.includes('possible restoration') ||
    c.includes('restored') || c.includes('recovery')
  ) return 'emerald';

  return 'muted';
}
