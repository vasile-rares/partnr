import { randomUUID } from "node:crypto";
import { expect, type APIRequestContext, type Page } from "@playwright/test";

export const PASSWORD = "correct horse battery";

// Every test gets its own account, so tests are independent and can run in parallel against the same database.
export const newUser = () => ({ email: `e2e-${randomUUID()}@example.com`, fullName: "Ana Pop", password: PASSWORD });

// Creates an account through the API without touching the browser session. The `request` fixture has its own cookie
// storage (unlike `page.request`), so the page stays signed out.
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
