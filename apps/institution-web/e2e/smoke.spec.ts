import { expect, test } from "@playwright/test";

// Shell navigation smoke. Asserts the Vite dev server boots, the
// router mounts the institution shell, every primary nav target
// reaches a distinct screen, and no runtime errors are logged. The
// full authenticated-flow E2E (#254 login + step-up) replaces this
// with a production-shaped happy path once auth ships.
test.describe("institution-web shell", () => {
  test("navigates between primary screens without runtime errors", async ({ page }) => {
    const errors: string[] = [];
    page.on("pageerror", (err) => errors.push(err.message));
    page.on("console", (msg) => {
      if (msg.type() === "error") {
        errors.push(msg.text());
      }
    });

    await page.goto("/");
    await expect(page.getByRole("heading", { level: 1, name: /overview/i })).toBeVisible();

    const targets: Array<{ link: RegExp; heading: RegExp }> = [
      { link: /live signals/i, heading: /live signals/i },
      { link: /areas/i, heading: /^areas$/i },
      { link: /metrics/i, heading: /metrics/i },
      { link: /^overview$/i, heading: /overview/i },
    ];

    const primaryNav = page.getByRole("navigation", { name: /primary/i });
    for (const { link, heading } of targets) {
      await primaryNav.getByRole("link", { name: link }).click();
      await expect(page.getByRole("heading", { level: 1, name: heading })).toBeVisible();
    }

    expect(errors, "no runtime errors across shell navigation").toEqual([]);
  });

  test("renders a recovery surface for unknown routes", async ({ page }) => {
    await page.goto("/this-route-does-not-exist");
    await expect(page.getByRole("alert")).toContainText(/page not found/i);
    await page.getByRole("link", { name: /return to overview/i }).click();
    await expect(page.getByRole("heading", { level: 1, name: /overview/i })).toBeVisible();
  });
});
