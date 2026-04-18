import { expect, test } from "@playwright/test";

// Shell + dashboard smoke. Asserts the Vite dev server boots, the
// router mounts the institution shell, every primary nav target
// reaches a distinct screen, and the Overview/Signals/Areas surfaces
// hydrate from stubbed institution routes without runtime errors.
// The full authenticated-flow E2E (#254 login + step-up) replaces
// this with a production-shaped happy path once auth ships.

const overviewPayload = {
  summary: {
    activeSignals: 4,
    growingSignals: 1,
    updatesPostedToday: 2,
    stabilisedToday: 1,
  },
  areas: [
    {
      id: "area-1",
      name: "Kilimani",
      condition: "degraded",
      activeSignals: 3,
      topCategory: "water",
      lastUpdatedAt: null,
    },
  ],
};

const signalsPayload = {
  items: [
    {
      id: "cluster-1",
      title: "Power outage on Ngong Road",
      area: { id: "area-1", name: "Kilimani" },
      category: "electricity",
      condition: "major",
      trend: "growing",
      responseStatus: "teams_dispatched",
      affectedCount: 12,
      recentReports24h: 14,
      timeActiveSeconds: 3600,
    },
  ],
  nextCursor: null,
};

const areasPayload = {
  items: [
    {
      id: "area-1",
      name: "Kilimani",
      condition: "degraded",
      activeSignals: 3,
      topCategory: "water",
      lastUpdatedAt: null,
    },
  ],
};

test.describe("institution-web dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await page.route("**/v1/institution/overview", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(overviewPayload),
      }),
    );
    await page.route("**/v1/institution/signals", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(signalsPayload),
      }),
    );
    await page.route("**/v1/institution/areas", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(areasPayload),
      }),
    );
  });

  test("navigates between primary screens and renders the wired data without runtime errors", async ({
    page,
  }) => {
    const errors: string[] = [];
    page.on("pageerror", (err) => errors.push(err.message));
    page.on("console", (msg) => {
      if (msg.type() === "error") {
        errors.push(msg.text());
      }
    });

    await page.goto("/");
    await expect(page.getByRole("heading", { level: 1, name: /overview/i })).toBeVisible();
    await expect(page.getByTestId("summary-active-signals")).toContainText("4");
    await expect(page.getByTestId("overview-area-item").first()).toContainText(/kilimani/i);

    const primaryNav = page.getByRole("navigation", { name: /primary/i });

    await primaryNav.getByRole("link", { name: /live signals/i }).click();
    await expect(page.getByRole("heading", { level: 1, name: /live signals/i })).toBeVisible();
    await expect(page.getByTestId("signal-row").first()).toContainText(
      /power outage on ngong road/i,
    );

    await primaryNav.getByRole("link", { name: /^areas$/i }).click();
    await expect(page.getByRole("heading", { level: 1, name: /^areas$/i })).toBeVisible();
    await expect(page.getByTestId("area-item").first()).toContainText(/kilimani/i);

    await primaryNav.getByRole("link", { name: /metrics/i }).click();
    await expect(page.getByRole("heading", { level: 1, name: /metrics/i })).toBeVisible();

    await primaryNav.getByRole("link", { name: /^overview$/i }).click();
    await expect(page.getByRole("heading", { level: 1, name: /overview/i })).toBeVisible();

    expect(errors, "no runtime errors across shell navigation").toEqual([]);
  });

  test("opens cluster detail from the signals list", async ({ page }) => {
    await page.route("**/v1/institution/signals/cluster-1", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          id: "cluster-1",
          state: "active",
          category: "electricity",
          subcategorySlug: "outage",
          title: "Power outage on Ngong Road",
          summary: "Several blocks along Ngong Road report no power.",
          affectedCount: 18,
          observingCount: 6,
          createdAt: "2026-04-18T03:00:00Z",
          updatedAt: "2026-04-18T05:00:00Z",
          activatedAt: "2026-04-18T03:30:00Z",
          possibleRestorationAt: null,
          resolvedAt: null,
          locationLabel: "Ngong Road near Adams Arcade, Kilimani",
          responseStatus: "teams_dispatched",
        }),
      }),
    );

    await page.goto("/signals");
    await page.getByRole("link", { name: /power outage on ngong road/i }).click();
    await expect(
      page.getByRole("heading", { level: 2, name: /power outage on ngong road/i }),
    ).toBeVisible();
    await expect(page.getByText(/ngong road near adams arcade/i)).toBeVisible();
  });

  test("renders a recovery surface for unknown routes", async ({ page }) => {
    await page.goto("/this-route-does-not-exist");
    await expect(page.getByRole("alert")).toContainText(/page not found/i);
    await page.getByRole("link", { name: /return to overview/i }).click();
    await expect(page.getByRole("heading", { level: 1, name: /overview/i })).toBeVisible();
  });
});
