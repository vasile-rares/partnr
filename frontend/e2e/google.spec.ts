import { expect, test, type Page } from "@playwright/test";
import { createAccount, createWorkspace, signIn } from "./helpers";

// The API runs with a stand-in for Google (Google__Fake, see start-api.mjs): "consent" sends the browser straight back with a code.

async function signedInFreelancer(page: Page, request: Parameters<typeof createAccount>[0]) {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  return { user, workspace };
}

// The state is what ties a return from Google to this account, so the tests that fake a different return need a real one.
async function newState(page: Page): Promise<string> {
  const response = await page.request.post("/api/integrations/google/connect");
  expect(response.ok()).toBe(true);
  const { url } = await response.json();

  return new URL(url).searchParams.get("state")!;
}

const googleCard = (page: Page) => page.getByRole("region", { name: "Google Calendar" });

test("a freelancer connects Google, sees which account it is, and disconnects", async ({ page, request }) => {
  await signedInFreelancer(page, request);
  await page.goto("/account");

  await expect(googleCard(page)).toContainText("Not connected");
  await expect(googleCard(page).getByRole("button", { name: "Disconnect" })).toHaveCount(0);

  await googleCard(page).getByRole("button", { name: "Connect Google Calendar" }).click();

  await expect(page).toHaveURL("/account?google=connected");
  await expect(page.getByRole("status").filter({ hasText: "Google Calendar connected" })).toBeVisible();
  await expect(googleCard(page)).toContainText("Connected as fake.user@gmail.example");
  await expect(googleCard(page).getByRole("button", { name: "Connect Google Calendar" })).toHaveCount(0);

  // The message belongs to the way back from Google; a plain visit shows only the state.
  await page.goto("/account");
  await expect(page.getByRole("status").filter({ hasText: "Google Calendar connected" })).toHaveCount(0);
  await expect(googleCard(page)).toContainText("Connected as fake.user@gmail.example");

  await googleCard(page).getByRole("button", { name: "Disconnect" }).click();
  await expect(page.getByRole("alertdialog")).toContainText("Meetings already in your calendar stay there");
  await page.getByRole("alertdialog").getByRole("button", { name: "Keep it connected" }).click();
  await expect(googleCard(page)).toContainText("Connected as");

  await googleCard(page).getByRole("button", { name: "Disconnect" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Disconnect" }).click();
  await expect(googleCard(page)).toContainText("Not connected");
  await expect(googleCard(page).getByRole("button", { name: "Connect Google Calendar" })).toBeVisible();
});

test("saying no on Google's screen leaves nothing connected and says so", async ({ page, request }) => {
  await signedInFreelancer(page, request);
  const state = await newState(page);

  await page.goto(`/api/integrations/google/callback?error=access_denied&state=${encodeURIComponent(state)}`);

  await expect(page).toHaveURL("/account?google=denied");
  await expect(page.getByRole("alert").filter({ hasText: "Google Calendar was not connected" })).toBeVisible();
  await expect(googleCard(page)).toContainText("Not connected");
});

test("unticking the calendar on Google's screen asks for the permission again", async ({ page, request }) => {
  await signedInFreelancer(page, request);
  const state = await newState(page);

  await page.goto(`/api/integrations/google/callback?code=fake-no-calendar&state=${encodeURIComponent(state)}`);

  await expect(page).toHaveURL("/account?google=permission");
  await expect(page.getByRole("alert").filter({ hasText: "Calendar access is missing" })).toBeVisible();
  await expect(googleCard(page)).toContainText("Not connected");
});

test("a return from Google that was not started here connects nothing", async ({ page, request }) => {
  await signedInFreelancer(page, request);

  await page.goto("/api/integrations/google/callback?code=fake-ok&state=forged");

  await expect(page).toHaveURL("/account?google=error");
  await expect(page.getByRole("alert").filter({ hasText: "Google Calendar could not be connected" })).toBeVisible();
  await expect(googleCard(page)).toContainText("Not connected");
});

test("an unknown ?google value is ignored, and someone with no workspace has no Google card", async ({ page, request }) => {
  const user = await createAccount(request);
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL("/workspaces/new");

  await page.goto("/account?google=connected");
  await expect(page.getByRole("heading", { level: 1, name: "Your profile" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Google Calendar" })).toHaveCount(0);
  await expect(page.getByText("Google Calendar connected")).toHaveCount(0);

  await createWorkspace(request, "Studio");
  await page.goto("/account?google=<script>");
  await expect(googleCard(page)).toBeVisible();
  await expect(page.getByRole("status").filter({ hasText: "Google Calendar" })).toHaveCount(0);
  await expect(page.getByRole("alert").filter({ hasText: "Google Calendar" })).toHaveCount(0);
});
