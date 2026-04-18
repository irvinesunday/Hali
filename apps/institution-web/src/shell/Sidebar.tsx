import { NavLink } from "react-router-dom";
import { primaryNavigation } from "./navigation";

// Fixed-width institution sidebar. Width is locked to 224px (w-56) to
// match the reconciled Phase 2 frame rule (224px rail + 64px topbar).
// Auth guard, user menu, and institution-switcher surface land in #254
// (auth) and later admin issues — the scaffold renders brand + nav.
export function Sidebar() {
  return (
    <aside
      aria-label="Primary navigation"
      className="flex w-56 shrink-0 flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground"
    >
      <div className="flex h-16 items-center gap-2 border-b border-sidebar-border px-5">
        <span
          aria-hidden
          className="inline-flex h-6 w-6 items-center justify-center rounded-sm bg-sidebar-primary text-[11px] font-semibold text-sidebar-primary-foreground"
        >
          H
        </span>
        <span className="text-sm font-semibold tracking-tight">Hali Institution</span>
      </div>

      <nav aria-label="Primary" className="flex-1 overflow-y-auto px-3 py-4">
        <ul className="space-y-1">
          {primaryNavigation.map((item) => (
            <li key={item.key}>
              <NavLink
                to={item.path}
                end={item.path === "/"}
                className={({ isActive }) =>
                  [
                    "flex items-center rounded-sm px-3 py-2 text-sm transition-colors",
                    isActive
                      ? "bg-sidebar-accent text-sidebar-accent-foreground font-medium"
                      : "text-sidebar-foreground hover:bg-sidebar-accent/60",
                  ].join(" ")
                }
              >
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  );
}
