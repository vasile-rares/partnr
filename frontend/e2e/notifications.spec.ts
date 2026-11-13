import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { PASSWORD, createAccount, mainNav } from "./helpers";
import { invitationTokenIn, waitForEmails, type ReceivedEmail } from "./mailpit";

// The API's worker looks for due emails every few seconds and sends them over SMTP; these tests only read the inbox in Mailpit.
const pathOf = (email: ReceivedEmail) => {
  const link = email.text.match(/https?:\/\/\S+/)?.[0];
  expect(link, "a link in the email").toBeTruthy();
  const url = new URL(link!);

  return url.pathname + url.search;
};

// The bell in the top bar; its name carries the unread count.
const bell = (page: Page) => page.getByRole("button", { name: /^Notifications/ });

// A studio with a client who joined and a shared deliverable, ready for an approval request.
async function studioWithClient(request: APIRequestContext, newClientContext: () => Promise<APIRequestContext>) {
  const studioUser = await createAccount(request);
  const clientEmail = `client-${randomUUID()}@example.com`;
  const workspace = await (await request.post("/api/workspaces", { data: { name: "Studio Pop" } })).json();
  const client = await (
    await request.post(`/api/workspaces/${workspace.id}/clients`, { data: { name: "Elena Popescu", email: clientEmail } })
  ).json();
  expect((await request.post(`/api/workspaces/${workspace.id}/clients/${client.id}/invitation`)).ok()).toBe(true);
  const token = invitationTokenIn((await waitForEmails(clientEmail))[0]);

  const clientRequest = await newClientContext();
  expect((await clientRequest.post(`/api/invitations/${token}/register`, { data: { fullName: "Elena Popescu", password: PASSWORD } })).ok()).toBe(true);

  const project = await (
    await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website" } })
  ).json();
  const projectUrl = `/api/workspaces/${workspace.id}/projects/${project.id}`;
  expect(
    (await request.put(projectUrl, { data: { name: "Website", description: null, startDate: null, dueDate: null, status: "Active" } })).ok(),
  ).toBe(true);
  const deliverable = await (
    await request.post(`${projectUrl}/deliverables`, { data: { title: "Homepage mockup", url: "https://files.example/file" } })
  ).json();
  expect((await request.post(`${projectUrl}/deliverables/${deliverable.id}/share`)).ok()).toBe(true);

  const ask = async (note: string | null) => {
    const asked = await request.post(`${projectUrl}/deliverables/${deliverable.id}/approvals`, { data: { note } });
    expect(asked.ok()).toBe(true);
    return (await asked.json()).approvals[0] as { id: string };
  };

  return { studioUser, clientEmail, workspace, client, project, deliverable, clientRequest, ask };
}

test("approvals and invitations email the person who has to know, with a link to the right page", async ({
  request,
  playwright,
  browser,
  baseURL,
}) => {
  const { studioUser, clientEmail, workspace, client, project, deliverable, clientRequest, ask } = await studioWithClient(request, () =>
    playwright.request.newContext({ baseURL }),
  );

  // Joining told the member who invited, and the link opens that client's page.
  const [joined] = await waitForEmails(studioUser.email);
  expect(joined.subject).toBe("Elena Popescu joined your client portal");
  expect(pathOf(joined)).toBe(`/w/${workspace.id}/clients/${client.id}`);

  // Asking for approval emails the client; the first email they got was the invitation.
  const round = await ask("Check the mobile layout.");
  const [, requested] = await waitForEmails(clientEmail, 2);
  expect(requested.subject).toBe("Studio Pop asked for your approval: Homepage mockup");
  expect(requested.text).not.toContain("Check the mobile layout."); // the note is read in the app
  expect(pathOf(requested)).toBe(`/portal/${workspace.id}/projects/${project.id}?tab=deliverables`);

  // The client's answer emails the member who asked, not the client.
  const decided = await clientRequest.post(
    `/api/portal/projects/${project.id}/deliverables/${deliverable.id}/approvals/${round.id}/approve`,
    { data: { message: "Looks great." } },
  );
  expect(decided.ok()).toBe(true);
  const [, approved] = await waitForEmails(studioUser.email, 2);
  expect(approved.subject).toBe("Elena Popescu approved Homepage mockup");
  expect(approved.text).not.toContain("Looks great.");
  expect(pathOf(approved)).toBe(`/w/${workspace.id}/projects/${project.id}?tab=deliverables`);

  // Both links open the page they promise, in the right app.
  const studio = await browser.newContext({ baseURL, storageState: await request.storageState() });
  const studioPage = await studio.newPage();
  await studioPage.goto(pathOf(approved));
  await expect(studioPage.getByRole("tab", { name: /Deliverables/ })).toHaveAttribute("aria-selected", "true");
  await studio.close();

  const asClient = await browser.newContext({ baseURL, storageState: await clientRequest.storageState() });
  const clientPage = await asClient.newPage();
  await clientPage.goto(pathOf(requested));
  await expect(clientPage.getByRole("tab", { name: /Deliverables/ })).toHaveAttribute("aria-selected", "true");
  await expect(clientPage.getByRole("region", { name: "Deliverables" })).toContainText("Homepage mockup");
  await asClient.close();
  await clientRequest.dispose();
});

test("the bell counts unread notifications, opens the one you click, and marks them all read", async ({
  request,
  playwright,
  browser,
  baseURL,
}) => {
  const { workspace, project, deliverable, clientRequest, ask } = await studioWithClient(request, () =>
    playwright.request.newContext({ baseURL }),
  );
  const round = await ask("Check the mobile layout.");

  // The client has one unread: the request. Opening it marks it read and leads to the deliverables.
  const asClient = await browser.newContext({ baseURL, storageState: await clientRequest.storageState() });
  const clientPage = await asClient.newPage();
  await clientPage.goto(`/portal/${workspace.id}`);
  await expect(bell(clientPage)).toHaveAccessibleName("Notifications, 1 unread");
  await bell(clientPage).click();
  const popover = clientPage.locator(".notifications-popover");
  await expect(popover.getByRole("listitem")).toHaveCount(1);
  await expect(popover).toContainText("Studio Pop asked for your approval: Homepage mockup");
  await popover.getByRole("button", { name: /Studio Pop asked for your approval/ }).click();
  await expect(clientPage).toHaveURL(`/portal/${workspace.id}/projects/${project.id}?tab=deliverables`);
  await expect(bell(clientPage)).toHaveAccessibleName("Notifications");

  // The freelancer gets the answer next to the notice about the client joining; "Mark all as read" clears both.
  const answered = await clientRequest.post(
    `/api/portal/projects/${project.id}/deliverables/${deliverable.id}/approvals/${round.id}/request-changes`,
    { data: { message: "The blue is too dark." } },
  );
  expect(answered.ok()).toBe(true);
  const studio = await browser.newContext({ baseURL, storageState: await request.storageState() });
  const studioPage = await studio.newPage();
  await studioPage.goto(`/w/${workspace.id}`);
  await expect(bell(studioPage)).toHaveAccessibleName("Notifications, 2 unread");
  await bell(studioPage).click();
  const inbox = studioPage.locator(".notifications-popover");
  await expect(inbox.getByRole("listitem")).toHaveCount(2);
  await expect(inbox.getByRole("listitem").first()).toContainText("Elena Popescu requested changes to Homepage mockup");
  await expect(inbox.getByRole("listitem").nth(1)).toContainText("Elena Popescu joined your client portal");
  await inbox.getByRole("button", { name: "Mark all as read" }).click();
  await expect(bell(studioPage)).toHaveAccessibleName("Notifications");
  await expect(inbox.getByRole("button", { name: "Mark all as read" })).toBeDisabled();
  await studioPage.keyboard.press("Escape");

  // A second request shows up next to the first; taking it back removes it from the client's inbox (and the unread count).
  const second = await ask("One more look?");
  await clientPage.reload();
  await expect(bell(clientPage)).toHaveAccessibleName("Notifications, 1 unread");
  await bell(clientPage).click();
  await expect(clientPage.locator(".notifications-popover").getByRole("listitem")).toHaveCount(2);
  await clientPage.keyboard.press("Escape");

  const withdrawn = await request.post(
    `/api/workspaces/${workspace.id}/projects/${project.id}/deliverables/${deliverable.id}/approvals/${second.id}/withdraw`,
  );
  expect(withdrawn.ok()).toBe(true);
  await clientPage.reload();
  await expect(bell(clientPage)).toHaveAccessibleName("Notifications");
  await bell(clientPage).click();
  await expect(clientPage.locator(".notifications-popover").getByRole("listitem")).toHaveCount(1);

  await asClient.close();
  await studio.close();
  await clientRequest.dispose();
});

// There is no polling: a bell that changes with no reload was told by the hub, through the web app's proxy.
test("the bell follows changes live, with no reload, and a second tab follows the first", async ({
  request,
  playwright,
  browser,
  baseURL,
}) => {
  const { workspace, project, deliverable, clientRequest, ask } = await studioWithClient(request, () =>
    playwright.request.newContext({ baseURL }),
  );
  const studio = await browser.newContext({ baseURL, storageState: await request.storageState() });
  const tabA = await studio.newPage();
  const tabB = await studio.newPage();
  await tabA.goto(`/w/${workspace.id}`);
  await tabB.goto(`/w/${workspace.id}/projects`);
  await expect(bell(tabA)).toHaveAccessibleName("Notifications, 1 unread"); // the client joining
  await expect(bell(tabB)).toHaveAccessibleName("Notifications, 1 unread");

  // The client answers while both tabs stay as they are.
  const asked = await ask("Check the mobile layout.");
  const answered = await clientRequest.post(
    `/api/portal/projects/${project.id}/deliverables/${deliverable.id}/approvals/${asked.id}/approve`,
    { data: {} },
  );
  expect(answered.ok()).toBe(true);
  await expect(bell(tabA)).toHaveAccessibleName("Notifications, 2 unread");
  await expect(bell(tabB)).toHaveAccessibleName("Notifications, 2 unread");

  // Reading them in one tab clears the other.
  await bell(tabA).click();
  await tabA.locator(".notifications-popover").getByRole("button", { name: "Mark all as read" }).click();
  await expect(bell(tabA)).toHaveAccessibleName("Notifications");
  await expect(bell(tabB)).toHaveAccessibleName("Notifications");
  await tabA.keyboard.press("Escape");

  // The client's side: a request arrives live, survives moving between pages, and leaves live when it is taken back.
  // (The first request is still unread for the client, so it is read first. A bell with no number also means "not loaded yet",
  // so what the test waits for is always a number.)
  expect((await clientRequest.post("/api/notifications/read-all")).ok()).toBe(true);
  const asClient = await browser.newContext({ baseURL, storageState: await clientRequest.storageState() });
  const clientPage = await asClient.newPage();
  await clientPage.goto(`/portal/${workspace.id}`);
  const second = await ask("One more look?");
  await expect(bell(clientPage)).toHaveAccessibleName("Notifications, 1 unread");
  await mainNav(clientPage).getByRole("link", { name: "Projects" }).click();
  await expect(clientPage).toHaveURL(`/portal/${workspace.id}/projects`);
  await expect(bell(clientPage)).toHaveAccessibleName("Notifications, 1 unread");
  const withdrawn = await request.post(
    `/api/workspaces/${workspace.id}/projects/${project.id}/deliverables/${deliverable.id}/approvals/${second.id}/withdraw`,
  );
  expect(withdrawn.ok()).toBe(true);
  await expect(bell(clientPage)).toHaveAccessibleName("Notifications");

  await asClient.close();
  await studio.close();
  await clientRequest.dispose();
});
