interface Props {
  className?: string
}

export default function WaveIcon({ className }: Props) {
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
      <path d="M2 10 C6 7 10 13 14 10 S22 7 26 10 S30 13 30 10" />
      <path d="M2 16 C6 13 10 19 14 16 S22 13 26 16 S30 19 30 16" />
      <path d="M2 22 C6 19 10 25 14 22 S22 19 26 22 S30 25 30 22" />
    </svg>
  )
}
