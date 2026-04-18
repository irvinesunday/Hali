interface Props {
  className?: string
}

export default function CheckIcon({ className }: Props) {
  return (
    <svg
      viewBox="0 0 40 40"
      className={className}
      fill="none"
      stroke="currentColor"
      strokeWidth="2.25"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <circle cx="20" cy="20" r="15" />
      <path d="M13 20.5 L18 25.5 L27 15.5" />
    </svg>
  )
}
