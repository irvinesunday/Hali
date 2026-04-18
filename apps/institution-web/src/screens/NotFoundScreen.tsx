import { Link } from "react-router-dom";

export function NotFoundScreen() {
  return (
    <section role="alert" className="max-w-md space-y-3">
      <h2 className="text-xl font-semibold text-foreground">Page not found</h2>
      <p className="text-sm text-muted-foreground">
        The route you followed is not part of the institution dashboard.
      </p>
      <Link to="/" className="text-sm font-medium text-primary hover:underline">
        Return to Overview
      </Link>
    </section>
  );
}
