// Institution dashboard topbar. 64px tall to match the reconciled
// shell rule. Holds the current screen title, an environment marker
// surfaced from VITE_APP_VERSION (and, later, a session/user menu
// wired in #254). Kept minimal here — business surfaces populate the
// right-hand slot in subsequent PRs.

interface TopbarProps {
  readonly title: string;
}

export function Topbar({ title }: TopbarProps) {
  const appVersion = import.meta.env.VITE_APP_VERSION ?? "local";
  return (
    <header className="sticky top-0 z-10 flex h-16 items-center justify-between border-b border-border bg-background px-6">
      <h1 className="text-base font-semibold text-foreground">{title}</h1>
      <div className="flex items-center gap-3 text-xs text-muted-foreground">
        <span>v{appVersion}</span>
      </div>
    </header>
  );
}
