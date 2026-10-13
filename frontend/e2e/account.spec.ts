import { expect, test } from "@playwright/test";
import { createAccount, signIn } from "./helpers";

test("the profile is edited from the account page and survives a reload", async ({ page, request }) => {
  const user = await createAccount(request);
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL("/workspaces/new");

  await page.getByRole("banner").getByRole("link", { name: "Account" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Your profile" })).toBeVisible();
  await expect(page.getByText(user.email)).toBeVisible();
  await expect(page.getByLabel("Full name")).toHaveValue(user.fullName);

  await page.getByLabel("Full name").fill("Ana Popescu");
  await page.getByLabel("Phone").fill("+40 721 000 000");
  await page.getByLabel("Company").fill("Acme SRL");
  await page.getByRole("button", { name: "Save profile" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Profile saved." })).toBeVisible();

  await page.reload();
  await expect(page.getByLabel("Full name")).toHaveValue("Ana Popescu");
  await expect(page.getByLabel("Phone")).toHaveValue("+40 721 000 000");
  await expect(page.getByLabel("Company")).toHaveValue("Acme SRL");

  // Clearing an optional detail removes it.
  await page.getByLabel("Company").fill("");
  await page.getByRole("button", { name: "Save profile" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Profile saved." })).toBeVisible();
  await page.reload();
  await expect(page.getByLabel("Company")).toHaveValue("");
});
