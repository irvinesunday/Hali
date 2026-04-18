// Small formatting helpers shared by the Live Signals list and the
// Signal Detail screen. Kept out of the component files so the test
// suite can pin the output directly without rendering React.

const SECONDS_IN_MINUTE = 60;
const SECONDS_IN_HOUR = 60 * SECONDS_IN_MINUTE;
const SECONDS_IN_DAY = 24 * SECONDS_IN_HOUR;

export function formatDurationSeconds(totalSeconds: number): string {
  if (!Number.isFinite(totalSeconds) || totalSeconds < 0) return "—";

  if (totalSeconds < SECONDS_IN_MINUTE) return `${Math.floor(totalSeconds)}s`;
  if (totalSeconds < SECONDS_IN_HOUR) {
    return `${Math.floor(totalSeconds / SECONDS_IN_MINUTE)}m`;
  }
  if (totalSeconds < SECONDS_IN_DAY) {
    const hours = Math.floor(totalSeconds / SECONDS_IN_HOUR);
    const minutes = Math.floor((totalSeconds % SECONDS_IN_HOUR) / SECONDS_IN_MINUTE);
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }

  const days = Math.floor(totalSeconds / SECONDS_IN_DAY);
  const hours = Math.floor((totalSeconds % SECONDS_IN_DAY) / SECONDS_IN_HOUR);
  return hours > 0 ? `${days}d ${hours}h` : `${days}d`;
}
