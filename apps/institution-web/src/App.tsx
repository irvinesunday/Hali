import { QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router-dom";
import { queryClient } from "./query/client";
import { router } from "./router";

// Application entrypoint. Mounts the TanStack Query cache provider so
// every screen shares one client, then the router that renders the
// institution shell and its screens. Auth gates (redirect to /login,
// step-up prompts) land in #254 as a wrapper inside each route
// element.
export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
}
