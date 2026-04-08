export function formatRelativeTime(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60_000);

  if (diffMins < 1) return 'just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays}d ago`;
}

export function formatLocationLabel(
  areaName?: string | null,
  roadName?: string | null,
  locationLabel?: string | null,
): string {
  if (locationLabel) return locationLabel;
  const parts = [roadName, areaName].filter(Boolean);
  return parts.length > 0 ? parts.join(', ') : 'Unknown location';
}

export function formatCategoryLabel(slug: string): string {
  return slug
    .split('_')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

/**
 * Returns a human-readable institution name for a civic category.
 * Used to populate OfficialUpdateRow when the API does not return
 * institution names directly. Nairobi-context defaults for MVP.
 */
export function getCategoryInstitutionName(category: string | null): string {
  switch (category?.toLowerCase()) {
    case 'electricity': return 'Kenya Power';
    case 'water':       return 'Nairobi Water';
    case 'roads':
    case 'transport':   return 'KURA / KeNHA';
    case 'environment':
    case 'governance':  return 'Nairobi County';
    case 'safety':      return 'County Government';
    case 'infrastructure': return 'County Government';
    default:            return 'Institution';
  }
}
