import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { createAccount, newUser, signIn } from "./helpers";
import { invitationTokenIn, waitForEmails } from "./mailpit";

// The freelancer's side goes through the API (tested elsewhere); these tests are about what the client sees.
// Each test uses its own client address: it is the key to the email in Mailpit.
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

  await page.goto("/");
  await expect(page).toHaveURL("/portal");

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

test("a client sees the projects shared with them, and what is done and what comes next", async ({ page, request }) => {
  await createAccount(request);
  const clientEmail = newClientEmail();
  const { workspace, client, token } = await inviteClient(request, "Studio Pop", clientEmail);

  const createProject = async (name: string, extra: object = {}) => {
    const response = await request.post(`/api/workspaces/${workspace.id}/projects`, {
      data: { clientId: client.id, name, ...extra },
    });
    expect(response.status()).toBe(201);
    return response.json();
  };
  // The update replaces every field, so the ones that must stay are sent back as they are.
  const setStatus = async (
    project: { id: string; name: string; description: string | null; startDate: string | null; dueDate: string | null },
    status: string,
  ) => {
    const { name, description, startDate, dueDate } = project;
    const response = await request.put(`/api/workspaces/${workspace.id}/projects/${project.id}`, {
      data: { name, description, startDate, dueDate, status },
    });
    expect(response.ok()).toBe(true);
  };
  const addStage = async (projectId: string, name: string, status: string) => {
    const added = await (await request.post(`/api/workspaces/${workspace.id}/projects/${projectId}/stages`, { data: { name } })).json();
    const stage = added.stages.at(-1);
    const response = await request.put(`/api/workspaces/${workspace.id}/projects/${projectId}/stages/${stage.id}`, {
      data: { name, status },
    });
    expect(response.ok()).toBe(true);
  };

  const website = await createProject("Website", { description: "A new site with five pages.", dueDate: "2026-12-15" });
  await addStage(website.id, "Design", "Done");
  await addStage(website.id, "Development", "InProgress");
  await addStage(website.id, "Launch", "ToDo");
  await setStatus(website, "Active");
  const draft = await createProject("Brand refresh");

  const addDeliverable = async (title: string, share: boolean) => {
    const created = await request.post(`/api/workspaces/${workspace.id}/projects/${website.id}/deliverables`, {
      data: { title, description: `About ${title}.`, url: `https://files.example/${encodeURIComponent(title)}` },
    });
    expect(created.status()).toBe(201);
    const deliverable = await created.json();
    if (share) {
      expect((await request.post(`/api/workspaces/${workspace.id}/projects/${website.id}/deliverables/${deliverable.id}/share`)).ok()).toBe(true);
    }
    return deliverable;
  };
  const mockup = await addDeliverable("Homepage mockup", true);
  await addDeliverable("Private notes", false);

  await page.goto(`/invite/${token}`);
  await page.getByLabel("Full name").fill("Elena Popescu");
  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByRole("button", { name: "Create account and accept" }).click();
  await expect(page).toHaveURL("/portal");

  const project = page.getByRole("link", { name: /Website/ });
  await expect(project).toContainText("1 of 3 done");
  await expect(project).toContainText("Active");
  await expect(project).toContainText("Due 15 Dec 2026");
  await expect(page.getByText("Brand refresh")).toHaveCount(0);

  await project.click();
  await expect(page).toHaveURL(`/portal/projects/${website.id}`);
  await expect(page.getByRole("heading", { level: 1, name: "Website" })).toBeVisible();
  await expect(page.getByText("A new site with five pages.")).toBeVisible();
  await expect(page.getByText("Working on Development.")).toBeVisible();
  await expect(page.getByRole("term").filter({ hasText: "Up next" })).toBeVisible();
  await expect(page.getByText("Launch").first()).toBeVisible();
  const stages = page.getByRole("region", { name: "Stages" }).getByRole("listitem");
  await expect(stages).toHaveCount(3);
  await expect(stages.nth(0)).toContainText("Design");
  await expect(stages.nth(0)).toContainText("Done");
  await expect(stages.nth(1)).toContainText("In progress");
  await expect(stages.nth(2)).toContainText("To do");
  await expect(page.getByText("Pays late.")).toHaveCount(0);

  // Only what was shared shows up, and it opens the freelancer's link in a new tab.
  const deliverables = page.getByRole("region", { name: "Deliverables" });
  await expect(deliverables.getByRole("listitem")).toHaveCount(1);
  const link = deliverables.getByRole("link", { name: /Homepage mockup/ });
  await expect(link).toHaveAttribute("href", "https://files.example/Homepage%20mockup");
  await expect(link).toHaveAttribute("target", "_blank");
  await expect(deliverables.getByText("About Homepage mockup.")).toBeVisible();
  await expect(page.getByText("Private notes")).toHaveCount(0);

  await request.post(`/api/workspaces/${workspace.id}/projects/${website.id}/deliverables/${mockup.id}/unshare`);
  await page.reload();
  await expect(page.getByRole("region", { name: "Deliverables" })).toHaveCount(0);

  // The draft has no page for the client until the freelancer moves it out of Planning.
  await page.goto(`/portal/projects/${draft.id}`);
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
  await setStatus(draft, "Active");
  await page.goto("/portal");
  await expect(page.getByRole("link", { name: /Brand refresh/ })).toContainText("No stages yet");
  await page.goto(`/portal/projects/${draft.id}`);
  await expect(page.getByRole("heading", { level: 1, name: "Brand refresh" })).toBeVisible();
  await expect(page.getByText("The stages of this project haven't been set up yet.")).toBeVisible();
});
