import { RouterProvider } from "react-router-dom";
import { router } from "./router";

// Application entrypoint. Mounts the institution dashboard router;
// the shell + placeholder screens under it are the deliverable of
// #201. Real business surfaces land in #202–#204, auth in #254.
export default function App() {
  return <RouterProvider router={router} />;
}
