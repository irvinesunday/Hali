import { Outlet, useLocation } from "react-router-dom";
import { primaryNavigation } from "./navigation";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";

// Layout shell for every authenticated institution route. Wraps the
// router <Outlet/> with the sidebar + topbar frame so individual
// screens (#202 onwards) can focus on their own content without
// re-implementing chrome. Session guarding (redirect to /login,
// redirect to /verify-totp, step-up prompts) lives in #254.
export function InstitutionShell() {
  const { pathname } = useLocation();
  const active =
    primaryNavigation.find(
      (item) => item.path === pathname || (item.path !== "/" && pathname.startsWith(item.path)),
    ) ?? primaryNavigation[0]!;

  return (
    <div className="flex min-h-full">
      <Sidebar />
      <div className="flex min-h-full flex-1 flex-col">
        <Topbar title={active.label} />
        <main className="flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
