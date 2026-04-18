import { expect, test } from "@playwright/test";

// Login flow E2E lands in issue #201. This scaffold spec asserts the
// Vite dev server boots and the application root renders without
// runtime errors.
test.describe("institution-web smoke", () => {
  test("renders the Hali Institution heading", async ({ page }) => {
    const errors: string[] = [];
    page.on("pageerror", (err) => errors.push(err.message));
    page.on("console", (msg) => {
      if (msg.type() === "error") {
        errors.push(msg.text());
      }
    });

    await page.goto("/");

    await expect(page.getByRole("heading", { level: 1, name: /hali institution/i })).toBeVisible();

    expect(errors, "no runtime errors on the index route").toEqual([]);
  });
});
