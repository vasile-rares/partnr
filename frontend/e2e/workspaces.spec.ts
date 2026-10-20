import { expect, test } from "@playwright/test";
import { createAccount, createWorkspace, signIn } from "./helpers";

const WORKSPACE_URL = /\/w\/[0-9a-f-]{36}$/;

test("a new account creates its first workspace and lands in it", async ({ page, request }) => {
  const user = await createAccount(request);
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page.getByRole("heading", { name: "Create your workspace" })).toBeVisible();

  await page.getByLabel("Workspace name").fill("Studio Pop");
  await page.getByRole("button", { name: "Create workspace" }).click();

  await expect(page).toHaveURL(WORKSPACE_URL);
  await expect(page.getByRole("heading", { level: 1, name: "Studio Pop" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Studio Pop" })).toBeVisible();
});

test("with one workspace, signing in goes straight into it", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio Pop");

  await page.goto("/login");
  await signIn(page, user.email, user.password);

  await expect(page).toHaveURL(`/w/${workspace.id}`);
});

test("with several workspaces, signing in shows the list to choose from", async ({ page, request }) => {
  const user = await createAccount(request);
  await createWorkspace(request, "Studio Pop");
  await createWorkspace(request, "Side project");

  await page.goto("/login");
  await signIn(page, user.email, user.password);

  await expect(page).toHaveURL("/workspaces");
  await expect(page.getByRole("link", { name: /Studio Pop/ })).toBeVisible();
  await page.getByRole("link", { name: /Side project/ }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Side project" })).toBeVisible();
});

test("someone else's workspace looks like it does not exist", async ({ page, request, playwright, baseURL }) => {
  await createAccount(request);
  const theirs = await createWorkspace(request, "Not yours");

  const strangerRequest = await playwright.request.newContext({ baseURL });
  const stranger = await createAccount(strangerRequest);
  await strangerRequest.dispose();
  await page.goto("/login");
  await signIn(page, stranger.email, stranger.password);
  await expect(page).toHaveURL("/workspaces/new");

  for (const path of [`/w/${theirs.id}`, `/w/${theirs.id}/settings`]) {
    await page.goto(path);
    await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
    await expect(page.getByText("Not yours")).toHaveCount(0);
  }
});

test("the owner renames the workspace and the new name shows everywhere", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.getByRole("link", { name: "Settings" }).click();
  await page.getByLabel("Workspace name").fill("Studio Pop SRL");
  await page.getByRole("button", { name: "Save name" }).click();

  await expect(page.getByRole("status").filter({ hasText: "Name saved." })).toBeVisible();
  await expect(page.getByRole("button", { name: "Studio Pop SRL" })).toBeVisible();
});
