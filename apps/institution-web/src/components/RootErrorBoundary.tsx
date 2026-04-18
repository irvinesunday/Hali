import { Component, type ErrorInfo, type ReactNode } from "react";

interface RootErrorBoundaryProps {
  children: ReactNode;
}

interface RootErrorBoundaryState {
  hasError: boolean;
}

// Application-root error boundary. Local error boundaries scoped to
// individual surfaces (signal list, signal detail, post-update modal,
// restoration CTA) land in later Phase 3 issues. This one is the last
// line of defence — if it fires, something escaped every other
// boundary and we show a neutral recovery surface.
//
// Error reporting transport (Sentry / equivalent) is wired in #207.
// For the scaffold we log to the console so runtime errors aren't
// silently swallowed by the boundary.
export class RootErrorBoundary extends Component<RootErrorBoundaryProps, RootErrorBoundaryState> {
  override state: RootErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(): RootErrorBoundaryState {
    return { hasError: true };
  }

  override componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error("[institution-web] Unhandled error reached the application root", error, info);
  }

  override render(): ReactNode {
    if (this.state.hasError) {
      return (
        <main className="flex min-h-full items-center justify-center p-8">
          <section
            role="alert"
            className="max-w-md space-y-3 rounded-md border border-border bg-card p-6 text-center"
          >
            <h1 className="text-lg font-semibold text-foreground">Something went wrong.</h1>
            <p className="text-sm text-muted-foreground">
              Reload the page to continue. If the issue persists, contact your Hali administrator.
            </p>
          </section>
        </main>
      );
    }

    return this.props.children;
  }
}
