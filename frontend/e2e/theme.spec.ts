import { expect, test } from "@playwright/test";

test("dark is the default, the theme can be switched, and the choice survives a reload", async ({ page }) => {
  await page.goto("/login");
  const html = page.locator("html");
  const barColour = page.locator('meta[name="theme-color"]');
  await expect(html).toHaveClass(/\bdark\b/);
  await expect(barColour).toHaveAttribute("content", "#181818");

  await page.getByRole("button", { name: "Change theme" }).click();
  await page.getByRole("menuitemradio", { name: "Light" }).click();
  await expect(html).not.toHaveClass(/\bdark\b/);
  await expect(barColour).toHaveAttribute("content", "#ffffff");

  await page.reload();
  await expect(html).not.toHaveClass(/\bdark\b/);

  await page.getByRole("button", { name: "Change theme" }).click();
  await page.getByRole("menuitemradio", { name: "Dark" }).click();
  await expect(html).toHaveClass(/\bdark\b/);
});
