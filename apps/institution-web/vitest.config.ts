/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

// Vitest config. We import `defineConfig` from `vite` rather than
// `vitest/config` because vitest 2's own `defineConfig` re-exports
// a Vite 5 PluginOption type, and the workspace now resolves vite@6
// (via the dev-dep bump in #253). The `/// <reference types>` pulls
// in the `test` block augmentation so Vitest options still typecheck.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    css: false,
    include: ["src/**/*.{test,spec}.{ts,tsx}"],
    exclude: ["node_modules", "dist", "e2e"],
  },
});
