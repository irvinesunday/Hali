interface Props {
  className?: string
}

export default function PeopleIcon({ className }: Props) {
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
      {/* Back person */}
      <circle cx="20" cy="10" r="4" />
      <path d="M12 28 C12 22 28 22 28 28" />
      {/* Front person */}
      <circle cx="12" cy="11" r="4" />
      <path d="M4 28 C4 22 20 22 20 28" />
    </svg>
  )
}
