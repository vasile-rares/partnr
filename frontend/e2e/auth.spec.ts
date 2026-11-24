import { expect, test } from "@playwright/test";
import { accountMenu, alertWith, createAccount, newUser, PASSWORD, signIn } from "./helpers";

const SESSION_COOKIE = ".AspNetCore.Identity.Application";

test("registering refuses an address that has an account, then creates one and asks for a first workspace", async ({ page, request }) => {
  const existing = await createAccount(request);
  const user = newUser();
  await page.goto("/register");
  await page.getByLabel("Full name").fill("Someone Else");
  await page.getByLabel("Email").fill(existing.email);
  await page.getByLabel("Password").fill(PASSWORD);
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(alertWith(page, "already exists")).toBeVisible();
  await expect(page).toHaveURL("/register");

  await page.getByLabel("Full name").fill(user.fullName);
  await page.getByLabel("Email").fill(user.email);
  await page.getByLabel("Password").fill(user.password);
  await page.getByRole("button", { name: "Create account" }).click();

  // The first registration of a run hits a cold stack (password hashing, the first server render), so it gets more than 5 s.
  await expect(page).toHaveURL("/workspaces/new", { timeout: 15_000 });
  await expect(page.getByRole("heading", { name: "Create your workspace" })).toBeVisible();
});

test("a wrong password shows an error and keeps the email, and a redirect target outside the site is ignored once it is right", async ({
  page,
  request,
}) => {
  const user = await createAccount(request);
  await page.goto("/login?next=//evil.example");

  await signIn(page, user.email, "not the password");

  await expect(alertWith(page, "Email or password is incorrect")).toBeVisible();
  await expect(page).toHaveURL(/\/login/);
  await expect(page.getByLabel("Email")).toHaveValue(user.email);

  await signIn(page, user.email, user.password);

  // Back on our own site: "/" then forwards a new account to its first workspace.
  await expect(page).toHaveURL("/workspaces/new");
});

test("a protected page sends you to sign in and back afterwards", async ({ page, request }) => {
  const user = await createAccount(request);

  await page.goto("/projects");
  await expect(page).toHaveURL("/login?next=%2Fprojects");

  await signIn(page, user.email, user.password);

  await expect(page).toHaveURL("/projects");
});

test("the session survives a reload, and signing out ends it", async ({ page, request }) => {
  const user = await createAccount(request);
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL("/workspaces/new");

  await page.reload();
  await expect(page.getByRole("heading", { name: "Create your workspace" })).toBeVisible();

  await accountMenu(page, user.email).click();
  await page.getByRole("menuitem", { name: "Sign out" }).click();
  await expect(page).toHaveURL("/login");

  await page.goto("/");
  await expect(page).toHaveURL("/login");
});

test("an invalid session cookie leads to the sign-in page without a redirect loop", async ({ page, context, baseURL }) => {
  await context.addCookies([{ name: SESSION_COOKIE, value: "not-a-real-session", url: baseURL }]);

  await page.goto("/");

  await expect(page).toHaveURL("/login");
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
});
