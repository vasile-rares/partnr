import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { createAccount, newUser, signIn } from "./helpers";
import { invitationTokenIn, waitForEmails } from "./mailpit";

// The freelancer's side is set up through the API (it has its own tests): these tests are about what the client sees.
// A client's address is the key to the email in Mailpit, so each test uses its own.
const newClientEmail = () => `client-${randomUUID()}@example.com`;

async function inviteClient(studio: APIRequestContext, workspaceName: string, clientEmail: string, clientName = "Elena Ionescu") {
  const workspace = await (await studio.post("/api/workspaces", { data: { name: workspaceName } })).json();
  const client = await (
    await studio.post(`/api/workspaces/${workspace.id}/clients`, { data: { name: clientName, email: clientEmail, notes: "Pays late." } })
  ).json();
  expect((await studio.post(`/api/workspaces/${workspace.id}/clients/${client.id}/invitation`)).ok()).toBe(true);

  const [email] = await waitForEmails(clientEmail);
  return { workspace, client, token: invitationTokenIn(email) };
}

test("someone without an account signs up through the invitation and lands in their portal", async ({ page, request }) => {
  await createAccount(request);
  const clientEmail = newClientEmail();
  const { workspace, client, token } = await inviteClient(request, "Studio Pop", clientEmail);

  await page.goto(`/invite/${token}`);
  await expect(page.getByRole("heading", { name: "Join Studio Pop" })).toBeVisible();
  // The address is the invited one and can't be changed; nothing the freelancer wrote about the client is filled in.
  await expect(page.getByLabel("Email")).toHaveValue(clientEmail);
  await expect(page.getByLabel("Email")).not.toBeEditable();
  await expect(page.getByLabel("Full name")).toHaveValue("");

  await page.getByLabel("Full name").fill("Elena Popescu");
  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByLabel("Phone (optional)").fill("+40 721 000 000");
  await page.getByLabel("Company (optional)").fill("Elena Design");
  await page.getByRole("button", { name: "Create account and accept" }).click();

  await expect(page).toHaveURL("/portal");
  await expect(page.getByRole("heading", { level: 1, name: "Your portal" })).toBeVisible();
  await expect(page.getByText("Studio Pop")).toBeVisible();

  // "/" now sends a client who has no workspace of their own to the portal, not to "Create your workspace".
  await page.goto("/");
  await expect(page).toHaveURL("/portal");

  // The freelancer sees the account's own details, while the notes stay theirs.
  const seen = await (await request.get(`/api/workspaces/${workspace.id}/clients/${client.id}`)).json();
  expect(seen).toMatchObject({
    name: "Elena Popescu",
    email: clientEmail,
    company: "Elena Design",
    phone: "+40 721 000 000",
    notes: "Pays late.",
    portal: { status: "Active" },
    detailsLocked: true,
  });
});

test("someone who already has an account signs in and accepts", async ({ page, request, playwright, baseURL }) => {
  const clientRequest = await playwright.request.newContext({ baseURL });
  const clientUser = await createAccount(clientRequest);
  await clientRequest.dispose();
  await createAccount(request);
  const { token } = await inviteClient(request, "Studio Pop", clientUser.email);

  await page.goto(`/invite/${token}`);
  await expect(page.getByRole("heading", { name: "Join Studio Pop" })).toBeVisible();
  await expect(page.getByLabel("Full name")).toHaveCount(0); // no sign-up form: the account is there already

  await page.getByRole("link", { name: "Sign in to accept" }).click();
  await signIn(page, clientUser.email, clientUser.password);
  await expect(page).toHaveURL(`/invite/${token}`);
  await page.getByRole("button", { name: "Accept invitation" }).click();

  await expect(page).toHaveURL("/portal");
  await expect(page.getByText("Studio Pop")).toBeVisible();

  // The same link doesn't work twice.
  await page.goto(`/invite/${token}`);
  await expect(page.getByRole("heading", { name: "Invitation already used" })).toBeVisible();
});

test("a different account can't accept the invitation, but can sign out and sign up with the invited address", async ({
  page,
  request,
  playwright,
  baseURL,
}) => {
  const otherRequest = await playwright.request.newContext({ baseURL });
  const other = await createAccount(otherRequest);
  await otherRequest.dispose();
  await createAccount(request);
  const clientEmail = newClientEmail();
  const { token } = await inviteClient(request, "Studio Pop", clientEmail);

  await page.goto("/login");
  await signIn(page, other.email, other.password);
  await expect(page).toHaveURL("/workspaces/new");

  await page.goto(`/invite/${token}`);
  await expect(page.getByText(`You are signed in as ${other.email}, but this invitation is for ${clientEmail}`)).toBeVisible();
  await expect(page.getByRole("button", { name: "Accept invitation" })).toHaveCount(0);

  await page.getByRole("button", { name: "Sign out and continue" }).click();
  await expect(page.getByLabel("Full name")).toBeVisible();
  await expect(page.getByLabel("Email")).toHaveValue(clientEmail);
});

test("a link that is invalid, cancelled or from a deleted client says so", async ({ page, request }) => {
  await createAccount(request);
  const { workspace, client, token } = await inviteClient(request, "Studio Pop", newClientEmail());

  await page.goto("/invite/not-a-real-token");
  await expect(page.getByRole("heading", { name: "Invitation not found" })).toBeVisible();
  await page.goto("/invite/..%2F..%2Fauth%2Fme");
  await expect(page.getByRole("heading", { name: "Invitation not found" })).toBeVisible();

  await request.delete(`/api/workspaces/${workspace.id}/clients/${client.id}/invitation`);
  await page.goto(`/invite/${token}`);
  await expect(page.getByRole("heading", { name: "Invitation cancelled" })).toBeVisible();
  await expect(page.getByText("Studio Pop cancelled this invitation")).toBeVisible();
});

test("the portal asks you to sign in, and is empty for someone who is nobody's client", async ({ page, request }) => {
  await page.goto("/portal");
  await expect(page).toHaveURL(/\/login\?next=%2Fportal$/);

  const user = newUser();
  await request.post("/api/auth/register", { data: user });
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL("/workspaces/new");
  await page.goto("/portal");
  await expect(page.getByText("You are not connected to anyone yet.")).toBeVisible();
});
