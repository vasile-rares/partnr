import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import { createAccount, createWorkspace, signIn } from "./helpers";
import { waitForEmails } from "./mailpit";

// The address is the key to the message in Mailpit, so every test uses its own.
const newClientEmail = () => `client-${randomUUID()}@example.com`;

test("a client is added from the clients page and shows up in the list", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.getByRole("banner").getByRole("link", { name: "Clients" }).click();
  await expect(page.getByText("No active clients")).toBeVisible();

  // Only an email is needed: the client is added and invited in one step, and the list shows the address as the name.
  const email = newClientEmail();
  await page.getByRole("link", { name: "Add client" }).click();
  await page.getByLabel("Email").fill(email.toUpperCase());
  await page.getByRole("button", { name: "Add and invite" }).click();

  await expect(page).toHaveURL(`/w/${workspace.id}/clients`);
  const row = page.getByRole("row", { name: new RegExp(email, "i") });
  await expect(row).toContainText("Invited");
  const [sent] = await waitForEmails(email);
  // No name was given, so the greeting has none (Mailpit returns the text with CRLF line breaks).
  expect(sent.text).toMatch(/^Hi,\s/);
});

test("a client can be added with a name too, and the list shows it", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  const email = newClientEmail();
  await page.goto(`/w/${workspace.id}/clients/new`);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Name (optional)").fill("Acme SRL");
  await page.getByRole("button", { name: "Add and invite" }).click();

  await expect(page).toHaveURL(`/w/${workspace.id}/clients`);
  await expect(page.getByRole("row", { name: /Acme SRL/ })).toContainText(email);
  const [sent] = await waitForEmails(email);
  expect(sent.text).toContain("Hi Acme SRL,");
});

test("suggestions offer existing accounts as the email is typed, and choosing one fills the name", async ({
  page,
  request,
  playwright,
  baseURL,
}) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const otherRequest = await playwright.request.newContext({ baseURL });
  const account = await createAccount(otherRequest);
  await otherRequest.dispose();
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/clients/new`);
  const start = account.email.slice(0, "e2e-".length + 12);
  await page.getByLabel("Email").fill(start);

  const option = page.getByRole("option", { name: new RegExp(account.fullName) });
  await expect(option).toContainText(account.email);
  await option.click();

  await expect(page.getByLabel("Email")).toHaveValue(account.email);
  await expect(page.getByLabel("Name (optional)")).toHaveValue(account.fullName);
  await expect(page.getByText(`Has an account: ${account.fullName}`)).toBeVisible();
});

test("an email without an account says so", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/clients/new`);
  await page.getByLabel("Email").fill(newClientEmail());

  await expect(page.getByText("No account with this email yet. They'll get an email to create one.")).toBeVisible();
  await expect(page.getByRole("option")).toHaveCount(0);
});

test("typing the email of someone who is already a client warns and blocks adding them again", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const email = newClientEmail();
  await request.post(`/api/workspaces/${workspace.id}/clients`, { data: { name: "First", email } });
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/clients/new`);
  await page.getByLabel("Email").fill(email.toUpperCase());

  await expect(page.getByText("Already a client in this workspace.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Add and invite" })).toBeDisabled();

  // Another address brings the button back.
  await page.getByLabel("Email").fill(newClientEmail());
  await expect(page.getByRole("button", { name: "Add and invite" })).toBeEnabled();
});

test("a client is edited, archived and restored, and the list follows", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const created = await request.post(`/api/workspaces/${workspace.id}/clients`, {
    data: { name: "Acme SRL", email: "contact@acme.com", phone: "0700" },
  });
  const acme = await created.json();
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  // Edit: rename, set a company and clear the phone.
  await page.goto(`/w/${workspace.id}/clients`);
  await page.getByRole("link", { name: "Acme SRL" }).click();
  await expect(page.getByLabel("Phone")).toHaveValue("0700");
  await page.getByLabel("Name").fill("Acme International");
  await page.getByLabel("Company").fill("Acme Group");
  await page.getByLabel("Phone").fill("");
  await page.getByRole("button", { name: "Save changes" }).click();

  await expect(page.getByRole("status").filter({ hasText: "Changes saved." })).toBeVisible();
  await expect(page.getByRole("heading", { level: 1, name: "Acme International" })).toBeVisible();

  // Archive: it leaves the default list, but the archived view still shows it.
  await page.getByRole("button", { name: "Archive client" }).click();
  await expect(page.getByText("This client is archived")).toBeVisible();
  await page.getByRole("link", { name: "← Clients" }).click();
  await expect(page.getByText("No active clients")).toBeVisible();
  await page.getByRole("link", { name: "Show archived clients" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/clients?archived=1`);
  await expect(page.getByRole("row", { name: /Acme International/ })).toContainText("Archived");

  // Restore: it is back in the default list.
  await page.goto(`/w/${workspace.id}/clients/${acme.id}`);
  await page.getByRole("button", { name: "Restore client" }).click();
  await expect(page.getByText("This client is archived")).toHaveCount(0);
  await page.getByRole("link", { name: "← Clients" }).click();
  await expect(page.getByRole("row", { name: /Acme International/ })).toContainText("Acme Group");
  await expect(page.getByRole("row", { name: /Acme International/ })).not.toContainText("Archived");
});

test("editing a client to another client's email shows the error under the email", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  await request.post(`/api/workspaces/${workspace.id}/clients`, { data: { name: "First", email: "first@acme.com" } });
  const second = await (
    await request.post(`/api/workspaces/${workspace.id}/clients`, { data: { name: "Second", email: "second@acme.com" } })
  ).json();
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/clients/${second.id}`);
  await page.getByLabel("Email").fill("FIRST@acme.com");
  await page.getByRole("button", { name: "Save changes" }).click();

  await expect(page.getByText("already exists in this workspace")).toBeVisible();
  await expect(page.getByLabel("Email")).toHaveValue("FIRST@acme.com");
});

test("a client that does not exist, or belongs to another workspace, looks missing", async ({ page, request }) => {
  const user = await createAccount(request);
  const mine = await createWorkspace(request, "Mine");
  const theirs = await createWorkspace(request, "Theirs");
  const hidden = await (
    await request.post(`/api/workspaces/${theirs.id}/clients`, { data: { name: "Secret Client", email: "s@acme.com" } })
  ).json();
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL("/workspaces");

  for (const clientId of [hidden.id, "00000000-0000-0000-0000-000000000000", "not-an-id"]) {
    await page.goto(`/w/${mine.id}/clients/${clientId}`);
    await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
    await expect(page.getByText("Secret Client")).toHaveCount(0);
  }
});
