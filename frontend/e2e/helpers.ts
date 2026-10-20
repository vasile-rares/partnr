import { randomUUID } from "node:crypto";
import { expect, type APIRequestContext, type Page } from "@playwright/test";

export const PASSWORD = "correct horse battery";

// Every test gets its own account, so tests are independent and can run in parallel against the same database.
export const newUser = () => ({ email: `e2e-${randomUUID()}@example.com`, fullName: "Ana Pop", password: PASSWORD });

// Creates an account without touching the browser session: the `request` fixture has its own cookie storage
// (unlike `page.request`), so the page stays signed out.
export async function createAccount(request: APIRequestContext) {
  const user = newUser();
  const response = await request.post("/api/auth/register", { data: user });
  expect(response.status()).toBe(201);
  return user;
}

export async function signIn(page: Page, email: string, password: string) {
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
}

// Next.js has its own (empty) role="alert" live region, so alerts are matched by their text.
export const alertWith = (page: Page, text: string) => page.getByRole("alert").filter({ hasText: text });

// Creates a workspace as the account last registered through this request context (it keeps the session cookie).
export async function createWorkspace(request: APIRequestContext, name: string): Promise<{ id: string; name: string }> {
  const response = await request.post("/api/workspaces", { data: { name } });
  expect(response.status()).toBe(201);
  return response.json();
}

// The sidebar's main menu: Home, Projects, Clients and the project shortcuts (Settings sits in the footer, outside it).
export const mainNav = (page: Page) => page.getByRole("navigation", { name: "Main" });

// The breadcrumb in the top bar of every page inside the app shell.
export const breadcrumb = (page: Page) => page.getByRole("navigation", { name: "breadcrumb" });

// The account menu at the bottom of the sidebar; its button carries the signed-in person's name and email.
export const accountMenu = (page: Page, email: string) => page.getByRole("button", { name: email });

// A project page keeps its sections in tabs; only the open one is on the page.
export const openTab = (page: Page, name: string) => page.getByRole("tab", { name }).click();

// The panel that edits a project (and archives it) opens from the button next to the project's name. It is modal:
// close it (Escape) before using anything behind it.
export async function openEditor(page: Page) {
  await page.getByRole("button", { name: "Edit project", exact: true }).click();
  return page.getByRole("dialog", { name: "Edit project" });
}
