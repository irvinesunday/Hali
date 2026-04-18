import { DesignSystemVersion } from "@hali/design-system";

// Smoke-check index route. Business UI, routing, auth, and data
// fetching are out of scope for the scaffold PR (#200). Issue #201
// replaces this placeholder with the institution shell and login flow.
export default function App() {
  return (
    <main className="flex min-h-full items-center justify-center p-8">
      <section className="max-w-md space-y-3 text-center">
        <p className="text-sm uppercase tracking-wide text-muted-foreground">Hali</p>
        <h1 className="text-3xl font-semibold text-foreground">Hali Institution</h1>
        <p className="text-sm text-muted-foreground">Design system v{DesignSystemVersion}</p>
      </section>
    </main>
  );
}
