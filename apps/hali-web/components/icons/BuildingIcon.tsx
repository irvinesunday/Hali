interface Props {
  className?: string
}

export default function BuildingIcon({ className }: Props) {
  return (
    <svg
      viewBox="0 0 32 32"
      className={className}
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {/* Building outline */}
      <rect x="4" y="6" width="24" height="22" rx="1" />
      {/* Roof line */}
      <line x1="4" y1="12" x2="28" y2="12" />
      {/* Windows row 1 */}
      <rect x="8" y="15" width="4" height="4" rx="0.5" />
      <rect x="20" y="15" width="4" height="4" rx="0.5" />
      {/* Windows row 2 */}
      <rect x="8" y="22" width="4" height="4" rx="0.5" />
      <rect x="20" y="22" width="4" height="4" rx="0.5" />
      {/* Center door */}
      <rect x="14" y="22" width="4" height="6" rx="0.5" />
    </svg>
  )
}
