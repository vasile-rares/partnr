import { expect, test } from "@playwright/test";
import { alertWith, createAccount, newUser, PASSWORD, signIn } from "./helpers";

const SESSION_COOKIE = ".AspNetCore.Identity.Application";

test("registering creates the account and asks for a first workspace", async ({ page }) => {
  const user = newUser();
  await page.goto("/register");

  await page.getByLabel("Full name").fill(user.fullName);
  await page.getByLabel("Email").fill(user.email);
  await page.getByLabel("Password").fill(user.password);
  await page.getByRole("button", { name: "Create account" }).click();

  // A new account has no workspace yet, so "/" sends it to create one.
  await expect(page).toHaveURL("/workspaces/new");
  await expect(page.getByRole("heading", { name: "Create your workspace" })).toBeVisible();
});

test("the session survives a reload", async ({ page, request }) => {
  const user = await createAccount(request);
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL("/workspaces/new");

  await page.reload();

  await expect(page.getByRole("heading", { name: "Create your workspace" })).toBeVisible();
});

test("signing out ends the session", async ({ page, request }) => {
  const user = await createAccount(request);
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL("/workspaces/new");

  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL("/login");

  await page.goto("/");
  await expect(page).toHaveURL("/login");
});

test("a wrong password shows an error and keeps the email", async ({ page, request }) => {
  const user = await createAccount(request);
  await page.goto("/login");

  await signIn(page, user.email, "not the password");

  await expect(alertWith(page, "Email or password is incorrect")).toBeVisible();
  await expect(page).toHaveURL("/login");
  await expect(page.getByLabel("Email")).toHaveValue(user.email);
});

test("registering with an email that already has an account shows an error", async ({ page, request }) => {
  const user = await createAccount(request);
  await page.goto("/register");

  await page.getByLabel("Full name").fill("Someone Else");
  await page.getByLabel("Email").fill(user.email);
  await page.getByLabel("Password").fill(PASSWORD);
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(alertWith(page, "already exists")).toBeVisible();
  await expect(page).toHaveURL("/register");
});

test("a protected page sends you to sign in and back afterwards", async ({ page, request }) => {
  const user = await createAccount(request);

  await page.goto("/projects");
  await expect(page).toHaveURL("/login?next=%2Fprojects");

  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL("/projects");
});

test("a redirect target outside the site is ignored", async ({ page, request }) => {
  const user = await createAccount(request);
  await page.goto("/login?next=//evil.example");

  await signIn(page, user.email, user.password);

  // Back on our own site: "/" then forwards a new account to its first workspace.
  await expect(page).toHaveURL("/workspaces/new");
});

test("an invalid session cookie leads to the sign-in page without a redirect loop", async ({ page, context, baseURL }) => {
  await context.addCookies([{ name: SESSION_COOKIE, value: "not-a-real-session", url: baseURL }]);

  await page.goto("/");

  await expect(page).toHaveURL("/login");
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
});
