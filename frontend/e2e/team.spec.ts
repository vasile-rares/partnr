import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import { createAccount, createWorkspace, mainNav, PASSWORD, signInAt } from "./helpers";
import { invitationTokenIn, waitForEmails, type ReceivedEmail } from "./mailpit";

// Each test invites its own, unique address: it is the key to the email in Mailpit.
const newEmail = () => `team-${randomUUID()}@example.com`;

const pathOf = (email: ReceivedEmail) => {
  const link = email.text.match(/https?:\/\/\S+/)?.[0];
  expect(link, "a link in the email").toBeTruthy();

  return new URL(link!).pathname;
};

test("the owner invites someone, who joins through the email, and then removes them", async ({ page, request, browser, baseURL }) => {
  const owner = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio Pop");
  const email = newEmail();

  await signInAt(page, owner, `/w/${workspace.id}`);
  await mainNav(page).getByRole("link", { name: "Team" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/team`);
  await expect(page.getByRole("heading", { level: 1, name: "Team" })).toBeVisible();
  await expect(page.getByText("No pending invitations.")).toBeVisible();

  await page.getByRole("button", { name: "Invite member" }).click();
  const dialog = page.getByRole("dialog", { name: "Invite a team member" });
  await dialog.getByLabel("Email").fill(email);
  await dialog.getByRole("button", { name: "Send invitation" }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("region", { name: "Pending invitations" }).getByText(email)).toBeVisible();

  // The invitee has no account: the link asks for a name and a password, and the address is the one it was sent to.
  const [invitation] = await waitForEmails(email);
  expect(invitation.subject).toBe("Studio Pop invited you to join their team");
  const newcomer = await browser.newContext({ baseURL });
  const newcomerPage = await newcomer.newPage();
  await newcomerPage.goto(`/invite/${invitationTokenIn(invitation)}`);
  await expect(newcomerPage.getByRole("heading", { name: "Join Studio Pop" })).toBeVisible();
  await expect(newcomerPage.getByText("to join their team")).toBeVisible();
  await expect(newcomerPage.getByLabel("Email")).toHaveValue(email);
  await newcomerPage.getByLabel("Full name").fill("Dana Member");
  await newcomerPage.getByLabel("Password").fill(PASSWORD);
  await newcomerPage.getByRole("button", { name: "Create account and accept" }).click();
  await expect(newcomerPage).toHaveURL(`/w/${workspace.id}`);

  // The owner was told, and the link opens the team.
  const [joined] = await waitForEmails(owner.email);
  expect(joined.subject).toBe("Dana Member joined your team");
  expect(pathOf(joined)).toBe(`/w/${workspace.id}/team`);

  await page.reload();
  const members = page.getByRole("region", { name: "Members" });
  await expect(members.getByRole("listitem")).toHaveCount(2);
  await expect(members.getByRole("listitem").first()).toContainText("Owner");
  await expect(members.getByRole("listitem").nth(1)).toContainText("Dana Member");
  await expect(members.getByRole("listitem").nth(1)).toContainText("Member");
  await expect(page.getByText("No pending invitations.")).toBeVisible();

  // Removing asks first; a click on "Keep them" changes nothing.
  await page.getByRole("button", { name: "Remove Dana Member" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Keep them" }).click();
  await expect(members.getByRole("listitem")).toHaveCount(2);

  await page.getByRole("button", { name: "Remove Dana Member" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Remove from the team" }).click();
  await expect(members.getByRole("listitem")).toHaveCount(1);

  // They lose the workspace at once.
  const gone = await newcomerPage.goto(`/w/${workspace.id}/team`);
  expect(gone?.status()).toBe(404);
});

test("a member reads the team but cannot invite or remove, and the API says so", async ({ page, request, browser, baseURL }) => {
  await createAccount(request);
  const workspace = await createWorkspace(request, "Studio Pop");
  const email = newEmail();

  expect((await request.post(`/api/workspaces/${workspace.id}/member-invitations`, { data: { email } })).status()).toBe(201);
  const token = invitationTokenIn((await waitForEmails(email))[0]);
  const memberContext = await browser.newContext({ baseURL });
  const joined = await memberContext.request.post(`/api/invitations/${token}/register`, { data: { fullName: "Mihai Member", password: PASSWORD } });
  expect(joined.ok()).toBe(true);

  const memberPage = await memberContext.newPage();
  await memberPage.goto(`/w/${workspace.id}/team`);
  await expect(memberPage.getByRole("heading", { level: 1, name: "Team" })).toBeVisible();
  const members = memberPage.getByRole("region", { name: "Members" });
  await expect(members.getByRole("listitem")).toHaveCount(2);
  await expect(members).toContainText("Mihai Member");
  await expect(members).toContainText("(you)");
  await expect(memberPage.getByText("Only the owner of this workspace can invite or remove people.")).toBeVisible();
  await expect(memberPage.getByRole("button", { name: "Invite member" })).toBeHidden();
  await expect(memberPage.getByRole("button", { name: /^Remove / })).toHaveCount(0);
  await expect(memberPage.getByRole("region", { name: "Pending invitations" })).toHaveCount(0);

  // The page hides the controls; the API is what refuses.
  expect((await memberContext.request.post(`/api/workspaces/${workspace.id}/member-invitations`, { data: { email: newEmail() } })).status()).toBe(403);
  expect((await memberContext.request.get(`/api/workspaces/${workspace.id}/member-invitations`)).status()).toBe(403);

  // Someone who is nobody's member does not even find the page.
  const stranger = await createAccount(request);
  await signInAt(page, stranger, "/workspaces/new");
  const hidden = await page.goto(`/w/${workspace.id}/team`);
  expect(hidden?.status()).toBe(404);
});

test("an invitation is resent, which replaces the link, and cancelled, which kills it", async ({ page, request, browser, baseURL }) => {
  const owner = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio Pop");
  const email = newEmail();
  expect((await request.post(`/api/workspaces/${workspace.id}/member-invitations`, { data: { email } })).status()).toBe(201);
  const firstToken = invitationTokenIn((await waitForEmails(email))[0]);

  await signInAt(page, owner, `/w/${workspace.id}/team`);
  const row = page.getByRole("region", { name: "Pending invitations" }).getByRole("listitem").filter({ hasText: email });
  await expect(row).toBeVisible();

  await page.getByRole("button", { name: `Resend invitation to ${email}` }).click();
  await expect(page.getByText(`Invitation sent to ${email}`)).toBeVisible();
  const [, resent] = await waitForEmails(email, 2);
  const secondToken = invitationTokenIn(resent);
  expect(secondToken).not.toBe(firstToken);

  const visitor = await (await browser.newContext({ baseURL })).newPage();
  await visitor.goto(`/invite/${firstToken}`);
  await expect(visitor.getByRole("heading", { name: "Invitation cancelled" })).toBeVisible();
  await visitor.goto(`/invite/${secondToken}`);
  await expect(visitor.getByRole("heading", { name: "Join Studio Pop" })).toBeVisible();

  await expect(row).toHaveCount(1);
  await page.getByRole("button", { name: `Cancel invitation to ${email}` }).click();
  await expect(page.getByText("No pending invitations.")).toBeVisible();
  await visitor.goto(`/invite/${secondToken}`);
  await expect(visitor.getByRole("heading", { name: "Invitation cancelled" })).toBeVisible();
});
