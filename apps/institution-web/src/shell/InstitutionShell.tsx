import { Outlet, useMatches } from "react-router-dom";
import { isRouteHandle } from "./routeHandle";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";

// Layout shell for every authenticated institution route. Wraps the
// router <Outlet/> with the sidebar + topbar frame so individual
// screens (#202 onwards) can focus on their own content without
// re-implementing chrome. Session guarding (redirect to /login,
// redirect to /verify-totp, step-up prompts) lives in #254.
//
// Topbar title comes from the deepest matched route's `handle.title`
// (see router.tsx). That keeps the shell correct for unmatched
// routes — the NotFound surface's title is "Page not found", not
// "Overview", which would otherwise mislead users on a 404.
export function InstitutionShell() {
  const matches = useMatches();
  const activeHandle = [...matches]
    .reverse()
    .map((match) => match.handle)
    .find(isRouteHandle);
  const title = activeHandle?.title ?? "Hali Institution";

  return (
    <div className="flex min-h-full">
      <Sidebar />
      <div className="flex min-h-full flex-1 flex-col">
        <Topbar title={title} />
        <main className="flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
