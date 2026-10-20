import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import { createAccount, createWorkspace, openClientEditor, signIn } from "./helpers";
import { invitationTokenIn, waitForEmails } from "./mailpit";

const newClientEmail = () => `client-${randomUUID()}@example.com`;

test("a client is invited, their details lock, and cancelling unlocks them again", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio Pop");
  const email = newClientEmail();
  const created = await request.post(`/api/workspaces/${workspace.id}/clients`, {
    data: { name: "Acme SRL", email, company: "Acme Group", notes: "Old note" },
  });
  const acme = await created.json();
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/clients/${acme.id}`);
  const portal = page.getByRole("region", { name: "Client portal" });
  await expect(portal.getByText("Not invited")).toBeVisible();
  const before = await openClientEditor(page);
  await expect(before.getByLabel("Name")).toBeEditable();
  await page.keyboard.press("Escape");

  await portal.getByRole("button", { name: "Send invitation" }).click();
  await expect(portal.getByRole("status")).toHaveText(`Invitation sent to ${email}.`);
  await expect(portal.getByText("Invited", { exact: true })).toBeVisible();
  await expect(portal.getByText(`An invitation was sent to ${email}`)).toBeVisible();
  const locked = await openClientEditor(page);
  for (const field of ["Name", "Email", "Company", "Phone"]) {
    await expect(locked.getByLabel(field)).not.toBeEditable();
  }
  await expect(locked.getByLabel("Name")).toHaveValue("Acme SRL");
  await locked.getByLabel("Notes").fill("New note");
  await locked.getByRole("button", { name: "Save notes" }).click();
  await expect(locked.getByRole("status")).toHaveText("Changes saved.");
  await page.keyboard.press("Escape");

  const [sent] = await waitForEmails(email);
  expect(sent.subject).toBe("Studio Pop invited you to follow your projects");
  expect(sent.text).toContain("http://localhost:3100/invite/");

  await page.goto(`/w/${workspace.id}/clients`);
  await expect(page.getByRole("row", { name: /Acme SRL/ })).toContainText("Invited");

  // Cancelling lifts the lock (this is how a wrong address gets corrected).
  await page.goto(`/w/${workspace.id}/clients/${acme.id}`);
  await portal.getByRole("button", { name: "Cancel invitation" }).click();
  await expect(portal.getByRole("status")).toHaveText("Invitation cancelled.");
  await expect(portal.getByText("Not invited")).toBeVisible();
  const unlocked = await openClientEditor(page);
  await expect(unlocked.getByLabel("Name")).toBeEditable();
  await expect(unlocked.getByLabel("Notes")).toHaveValue("New note");
  await unlocked.getByLabel("Email").fill(newClientEmail());
  await unlocked.getByRole("button", { name: "Save changes" }).click();
  await expect(unlocked.getByRole("status")).toHaveText("Changes saved.");
});

test("sending the invitation again replaces the first link", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio Pop");
  const email = newClientEmail();
  const created = await request.post(`/api/workspaces/${workspace.id}/clients`, { data: { name: "Acme SRL", email } });
  const acme = await created.json();
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/clients/${acme.id}`);
  const portal = page.getByRole("region", { name: "Client portal" });
  await portal.getByRole("button", { name: "Send invitation" }).click();
  await expect(portal.getByRole("status")).toHaveText(`Invitation sent to ${email}.`);
  await portal.getByRole("button", { name: "Resend invitation" }).click();
  await expect(portal.getByRole("status")).toHaveText(`Invitation sent to ${email}.`);

  const [first, second] = await waitForEmails(email, 2);
  const status = async (token: string) => ((await (await request.get(`/api/invitations/${token}`)).json()) as { status: string }).status;
  expect(await status(invitationTokenIn(first))).toBe("Cancelled");
  expect(await status(invitationTokenIn(second))).toBe("Pending");
});

test("an archived client cannot be invited", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio Pop");
  const created = await request.post(`/api/workspaces/${workspace.id}/clients`, {
    data: { name: "Old Client", email: newClientEmail() },
  });
  const old = await created.json();
  await request.post(`/api/workspaces/${workspace.id}/clients/${old.id}/archive`);
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/clients/${old.id}`);

  const portal = page.getByRole("region", { name: "Client portal" });
  await expect(portal.getByText("Restore this client to invite them to the portal.")).toBeVisible();
  await expect(portal.getByRole("button")).toHaveCount(0);
});
