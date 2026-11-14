import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import { createAccount } from "./helpers";
import { waitForEmails } from "./mailpit";

const resetPathIn = (text: string) => {
  const link = text.match(/https?:\/\/\S+\/reset-password\/[A-Za-z0-9_-]+/)?.[0];
  expect(link, "a reset link in the email").toBeTruthy();

  return new URL(link!).pathname;
};

test("a forgotten password is reset from the emailed link, which signs in here, signs out elsewhere and works once", async ({
  page,
  request,
}) => {
  const user = await createAccount(request); // `request` keeps that session: the "other device"
  expect((await request.get("/api/auth/me")).ok()).toBe(true);

  await page.goto("/login");
  await page.getByRole("link", { name: "Forgot password?" }).click();
  await expect(page).toHaveURL("/forgot-password");
  await page.getByLabel("Email").fill(user.email);
  await page.getByRole("button", { name: "Send the link" }).click();
  await expect(page.getByRole("status")).toContainText(`If an account exists for ${user.email}`);

  const [email] = await waitForEmails(user.email);
  expect(email.subject).toBe("Reset your Partnr password");
  const path = resetPathIn(email.text);

  await page.goto(path);
  await expect(page.getByRole("heading", { name: "Choose a new password" })).toBeVisible();
  await expect(page.getByText(`For ${user.email}`)).toBeVisible();
  await page.getByLabel("New password").fill("a brand new password");
  await page.getByRole("button", { name: "Set the new password" }).click();

  // Signed in here (no workspace yet, so the app asks for one), signed out on the other device.
  await expect(page).toHaveURL("/workspaces/new");
  expect((await request.get("/api/auth/me")).status()).toBe(401);

  await page.goto(path);
  await expect(page.getByRole("heading", { name: "Link can't be used" })).toBeVisible();
  await expect(page.getByText("already used")).toBeVisible();
});

test("an address without an account gets the same answer, and a made-up link says it is not valid", async ({ page }) => {
  await page.goto("/forgot-password");
  const nobody = `nobody-${randomUUID()}@example.com`;
  await page.getByLabel("Email").fill(nobody);
  await page.getByRole("button", { name: "Send the link" }).click();
  await expect(page.getByRole("status")).toContainText(`If an account exists for ${nobody}`);

  await page.goto("/reset-password/not-one-of-ours");
  await expect(page.getByRole("heading", { name: "Link not valid" })).toBeVisible();
  await page.getByRole("link", { name: "Ask for a new link" }).click();
  await expect(page).toHaveURL("/forgot-password");
});
