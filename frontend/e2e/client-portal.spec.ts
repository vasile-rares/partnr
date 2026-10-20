import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { alertWith, createAccount, mainNav, newUser, signIn } from "./helpers";
import { invitationTokenIn, waitForEmails } from "./mailpit";

// The freelancer's side goes through the API (tested elsewhere); these tests are about what the client sees.
// Each test uses its own client address: it is the key to the email in Mailpit.
const newClientEmail = () => `client-${randomUUID()}@example.com`;

// `nth` picks the invitation to return when the same address is invited by several studios (they arrive in order).
async function inviteClient(studio: APIRequestContext, workspaceName: string, clientEmail: string, clientName = "Elena Ionescu", nth = 1) {
  const workspace = await (await studio.post("/api/workspaces", { data: { name: workspaceName } })).json();
  const client = await (
    await studio.post(`/api/workspaces/${workspace.id}/clients`, { data: { name: clientName, email: clientEmail, notes: "Pays late." } })
  ).json();
  expect((await studio.post(`/api/workspaces/${workspace.id}/clients/${client.id}/invitation`)).ok()).toBe(true);

  const emails = await waitForEmails(clientEmail, nth);
  return { workspace, client, token: invitationTokenIn(emails[nth - 1]) };
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

  await expect(page).toHaveURL(`/portal/${workspace.id}`);
  await expect(page.getByRole("heading", { level: 1, name: "Studio Pop" })).toBeVisible();

  await page.goto("/");
  await expect(page).toHaveURL(`/portal/${workspace.id}`);

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
  const { workspace, token } = await inviteClient(request, "Studio Pop", clientUser.email);

  await page.goto(`/invite/${token}`);
  await expect(page.getByRole("heading", { name: "Join Studio Pop" })).toBeVisible();
  await expect(page.getByLabel("Full name")).toHaveCount(0); // no sign-up form: the account is there already

  await page.getByRole("link", { name: "Sign in to accept" }).click();
  await signIn(page, clientUser.email, clientUser.password);
  await expect(page).toHaveURL(`/invite/${token}`);
  await page.getByRole("button", { name: "Accept invitation" }).click();

  await expect(page).toHaveURL(`/portal/${workspace.id}`);
  await expect(page.getByRole("heading", { level: 1, name: "Studio Pop" })).toBeVisible();

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

test("a client reads the updates the freelancer shared, newest first, with the earlier ones tucked away", async ({ page, request }) => {
  await createAccount(request);
  const clientEmail = newClientEmail();
  const { workspace, client, token } = await inviteClient(request, "Studio Pop", clientEmail);
  const created = await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website" } });
  const website = await created.json();
  const projectUrl = `/api/workspaces/${workspace.id}/projects/${website.id}`;
  expect(
    (
      await request.put(projectUrl, {
        data: { name: "Website", description: null, startDate: null, dueDate: null, status: "Active" },
      })
    ).ok(),
  ).toBe(true);

  await page.goto(`/invite/${token}`);
  await page.getByLabel("Full name").fill("Elena Popescu");
  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByRole("button", { name: "Create account and accept" }).click();
  await expect(page).toHaveURL(`/portal/${workspace.id}`);

  await page.goto(`/portal/${workspace.id}/projects/${website.id}`);
  const updates = page.getByRole("region", { name: "Updates" });
  await expect(updates.getByText("No updates yet. Studio Pop will post news here as the project moves.")).toBeVisible();

  const addUpdate = async (title: string, body: string, share: boolean) => {
    const response = await request.post(`${projectUrl}/updates`, { data: { title, body } });
    expect(response.status()).toBe(201);
    const update = await response.json();
    if (share) {
      expect((await request.post(`${projectUrl}/updates/${update.id}/share`)).ok()).toBe(true);
    }
    return update;
  };
  for (let n = 1; n <= 7; n++) {
    await addUpdate(`Update ${n}`, n === 7 ? "Layouts are signed off.\nBuild starts Monday." : `Details of ${n}.`, true);
  }
  await addUpdate("Secret draft", "Not for the client.", false);

  // The five most recent are open, the other two wait behind a native disclosure.
  await page.reload();
  await expect(updates.getByRole("listitem")).toHaveCount(5);
  await expect(updates.getByRole("listitem").first()).toContainText("Update 7");
  await expect(updates.getByRole("listitem").first()).toContainText("Build starts Monday.");
  await expect(updates.getByRole("listitem").first().getByRole("time")).toHaveText(/^\d{1,2} \w{3} \d{4}$/);
  await expect(updates.getByText("Update 3", { exact: true })).toBeVisible();
  await expect(updates.getByText("Update 2", { exact: true })).toBeHidden();
  await expect(page.getByText("Secret draft")).toHaveCount(0);

  await updates.getByText("Show earlier updates (2)").click();
  await expect(updates.getByText("Update 2", { exact: true })).toBeVisible();
  await expect(updates.getByText("Update 1", { exact: true })).toBeVisible();
});

test("a client approves one deliverable and asks for changes on another, and is asked again after the freelancer replies", async ({
  page,
  request,
}) => {
  await createAccount(request);
  const clientEmail = newClientEmail();
  const { workspace, client, token } = await inviteClient(request, "Studio Pop", clientEmail);
  const created = await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website" } });
  const website = await created.json();
  const projectUrl = `/api/workspaces/${workspace.id}/projects/${website.id}`;
  expect(
    (await request.put(projectUrl, { data: { name: "Website", description: null, startDate: null, dueDate: null, status: "Active" } })).ok(),
  ).toBe(true);

  const addShared = async (title: string) => {
    const deliverable = await (await request.post(`${projectUrl}/deliverables`, { data: { title, url: "https://files.example/file" } })).json();
    expect((await request.post(`${projectUrl}/deliverables/${deliverable.id}/share`)).ok()).toBe(true);
    return deliverable;
  };
  const ask = async (deliverable: { id: string }, note: string) => {
    const response = await request.post(`${projectUrl}/deliverables/${deliverable.id}/approvals`, { data: { note } });
    expect(response.ok()).toBe(true);
  };
  const homepage = await addShared("Homepage mockup");
  const logo = await addShared("Logo");
  await ask(homepage, "Check the mobile layout.");
  await ask(logo, "Is this the right blue?");

  await page.goto(`/invite/${token}`);
  await page.getByLabel("Full name").fill("Elena Popescu");
  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByRole("button", { name: "Create account and accept" }).click();
  await expect(page).toHaveURL(`/portal/${workspace.id}`);
  await expect(page.getByRole("main").getByRole("link", { name: /Website/ })).toContainText("2 waiting for you");

  await page.getByRole("main").getByRole("link", { name: /Website/ }).click();
  const banner = page.getByRole("status").filter({ hasText: "Waiting for you" });
  await expect(banner).toContainText("2 deliverables need your approval.");
  const deliverables = page.getByRole("region", { name: "Deliverables" });
  const home = deliverables.getByRole("listitem").filter({ hasText: "Homepage mockup" });
  const logoItem = deliverables.getByRole("listitem").filter({ hasText: "Logo" });
  await expect(home).toContainText("Awaiting approval");
  await expect(home).toContainText("Studio Pop asks: “Check the mobile layout.”");

  // Approving is final, so it asks first; backing out sends nothing.
  await home.getByLabel("Your message").fill("Looks great.");
  await home.getByRole("button", { name: "Approve Homepage mockup" }).click();
  await page.getByRole("button", { name: "Not yet" }).click();
  await expect(home).toContainText("Awaiting approval");
  await home.getByRole("button", { name: "Approve Homepage mockup" }).click();
  await page.getByRole("button", { name: "Yes, approve" }).click();
  await expect(home).toContainText("You said: “Looks great.”");
  await expect(home).not.toContainText("Awaiting approval");
  await expect(home.getByRole("button", { name: "Approve Homepage mockup" })).toBeHidden();
  await expect(banner).toContainText("1 deliverable needs your approval.");

  // Asking for changes needs a reason.
  await logoItem.getByRole("button", { name: "Request changes to Logo" }).click();
  await expect(alertWith(page, "Say what should change.")).toBeVisible();
  await logoItem.getByLabel("Your message").fill("The blue is too dark.");
  await logoItem.getByRole("button", { name: "Request changes to Logo" }).click();
  await expect(logoItem).toContainText("Changes requested");
  await expect(logoItem).toContainText("You said: “The blue is too dark.”");
  await expect(banner).toHaveCount(0);
  await page.goto(`/portal/${workspace.id}`);
  await expect(page.getByRole("main").getByRole("link", { name: /Website/ })).not.toContainText("waiting for you");

  // The freelancer sees both answers, replies to the changes, and asks again: the earlier answer stays as history.
  const seen = await (await request.get(`${projectUrl}/deliverables/${logo.id}`)).json();
  expect([seen.approvalStatus, seen.approvals[0].message]).toEqual(["ChangesRequested", "The blue is too dark."]);
  await ask(logo, "Bluer now?");
  await page.goto(`/portal/${workspace.id}/projects/${website.id}`);
  await expect(logoItem).toContainText("Awaiting approval");
  await expect(logoItem).toContainText("Studio Pop asks: “Bluer now?”");
  await logoItem.getByText("Earlier answers (1)").click();
  await expect(logoItem).toContainText("You said: “The blue is too dark.”");
  await expect(banner).toContainText("1 deliverable needs your approval.");
  await page.goto(`/portal/${workspace.id}`);
  await expect(page.getByRole("main").getByRole("link", { name: /Website/ })).toContainText("1 waiting for you");
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
  await expect(page).toHaveURL(`/portal/${workspace.id}`);

  const project = page.getByRole("main").getByRole("link", { name: /Website/ });
  await expect(project).toContainText("1 of 3 done");
  await expect(project).toContainText("Active");
  await expect(project).toContainText("Due 15 Dec 2026");
  await expect(page.getByText("Brand refresh")).toHaveCount(0);

  await project.click();
  await expect(page).toHaveURL(`/portal/${workspace.id}/projects/${website.id}`);
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
  await page.goto(`/portal/${workspace.id}/projects/${draft.id}`);
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
  await setStatus(draft, "Active");
  await page.goto(`/portal/${workspace.id}`);
  await expect(page.getByRole("main").getByRole("link", { name: /Brand refresh/ })).toContainText("No stages yet");
  await page.goto(`/portal/${workspace.id}/projects/${draft.id}`);
  await expect(page.getByRole("heading", { level: 1, name: "Brand refresh" })).toBeVisible();
  await expect(page.getByText("The stages of this project haven't been set up yet.")).toBeVisible();
});

test("a client of two firms moves between their portals from the sidebar, and a project opens only under its own firm", async ({
  page,
  request,
  playwright,
  baseURL,
}) => {
  const clientEmail = newClientEmail();
  await createAccount(request);
  const studioPop = await inviteClient(request, "Studio Pop", clientEmail);
  const brandRequest = await playwright.request.newContext({ baseURL });
  await createAccount(brandRequest);
  const brandLab = await inviteClient(brandRequest, "Brand Lab", clientEmail, "Elena Ionescu", 2);

  const startProject = async (studio: APIRequestContext, firm: typeof studioPop, name: string) => {
    const project = await (
      await studio.post(`/api/workspaces/${firm.workspace.id}/projects`, { data: { clientId: firm.client.id, name } })
    ).json();
    const shared = await studio.put(`/api/workspaces/${firm.workspace.id}/projects/${project.id}`, {
      data: { name, description: null, startDate: null, dueDate: null, status: "Active" },
    });
    expect(shared.ok()).toBe(true);
    return project;
  };
  await startProject(request, studioPop, "Website");
  const logo = await startProject(brandRequest, brandLab, "Logo");
  await brandRequest.dispose();

  await page.goto(`/invite/${studioPop.token}`);
  await page.getByLabel("Full name").fill("Elena Popescu");
  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByRole("button", { name: "Create account and accept" }).click();
  await expect(page).toHaveURL(`/portal/${studioPop.workspace.id}`);

  // Signed in with the invited address, so the second invitation is one click.
  await page.goto(`/invite/${brandLab.token}`);
  await page.getByRole("button", { name: "Accept invitation" }).click();
  await expect(page).toHaveURL(/\/portal\/[0-9a-f-]{36}$/);

  // Each portal shows only its own firm's projects, in the list and in the sidebar.
  await page.goto(`/portal/${studioPop.workspace.id}`);
  await expect(page.getByRole("heading", { level: 1, name: "Studio Pop" })).toBeVisible();
  await expect(page.getByRole("main").getByRole("link", { name: /Website/ })).toBeVisible();
  await expect(page.getByRole("main").getByRole("link", { name: /Logo/ })).toHaveCount(0);
  await expect(mainNav(page).getByRole("link", { name: "Website" })).toBeVisible();
  await expect(mainNav(page).getByRole("link", { name: "Logo" })).toHaveCount(0);

  await page.getByRole("button", { name: "Studio Pop" }).click();
  await page.getByRole("menuitem", { name: "Brand Lab" }).click();
  await expect(page).toHaveURL(`/portal/${brandLab.workspace.id}`);
  await expect(page.getByRole("heading", { level: 1, name: "Brand Lab" })).toBeVisible();
  await expect(page.getByRole("main").getByRole("link", { name: /Logo/ })).toBeVisible();
  await expect(page.getByRole("main").getByRole("link", { name: /Website/ })).toHaveCount(0);
  await expect(mainNav(page).getByRole("link", { name: "Logo" })).toBeVisible();

  await page.getByRole("main").getByRole("link", { name: /Logo/ }).click();
  await expect(page).toHaveURL(`/portal/${brandLab.workspace.id}/projects/${logo.id}`);
  await expect(page.getByRole("heading", { level: 1, name: "Logo" })).toBeVisible();

  // The same project under the other firm's address, and a firm the client does not belong to, are both not found.
  await page.goto(`/portal/${studioPop.workspace.id}/projects/${logo.id}`);
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
  await page.goto(`/portal/${randomUUID()}`);
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});
