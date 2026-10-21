import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { alertWith, createAccount, createWorkspace, mainNav, openEditor, openTab, signIn } from "./helpers";

// Added through the API, without an invitation: a project can be prepared before the client is invited.
async function addClient(request: APIRequestContext, workspaceId: string, name: string): Promise<{ id: string }> {
  const response = await request.post(`/api/workspaces/${workspaceId}/clients`, {
    data: { name, email: `client-${randomUUID()}@example.com` },
  });
  expect(response.status()).toBe(201);
  return response.json();
}

test("a project is started from a client's page, moved out of Planning, and archived", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await addClient(request, workspace.id, "Acme SRL");
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/clients/${client.id}`);
  await expect(page.getByText("No projects for this client yet.")).toBeVisible();
  await page.getByRole("button", { name: "New project" }).click();
  const dialog = page.getByRole("dialog", { name: "New project" });
  await expect(dialog.getByLabel("Client", { exact: true })).toHaveValue(client.id);
  // The client's page wraps everything in the edit Sheet, which makes this dialog "nested" for Base UI.
  await expect(page.locator('[data-slot="dialog-overlay"]')).toBeVisible();

  await dialog.getByLabel("Name", { exact: true }).fill("Website");
  await dialog.getByLabel("Due date").fill("2026-12-15");
  await dialog.getByRole("button", { name: "Create project" }).click();

  await expect(page.getByRole("heading", { level: 1, name: "Website" })).toBeVisible();
  await expect(page.getByText("Not visible in their portal yet")).toBeVisible();

  const editor = await openEditor(page);
  await editor.getByLabel("Status").selectOption("Active");
  await editor.getByRole("button", { name: "Save changes" }).click();
  await expect(editor.getByRole("status")).toHaveText("Changes saved.");
  await page.keyboard.press("Escape");
  await expect(page.getByText("Not visible in their portal yet")).toBeHidden();

  await mainNav(page).getByRole("link", { name: "Projects", exact: true }).click();
  const row = page.getByRole("row", { name: /Website/ });
  await expect(row).toContainText("Acme SRL");
  await expect(row).toContainText("Active");
  await expect(row).toContainText("15 Dec 2026");

  await row.getByRole("link", { name: "Website" }).click();
  const archiving = await openEditor(page);
  await archiving.getByRole("button", { name: "Archive project" }).click();
  await expect(page.getByText("This project is archived")).toBeVisible();
  await expect(archiving.getByLabel("Name", { exact: true })).toBeDisabled();
  await expect(archiving.getByRole("button", { name: "Save changes" })).toBeHidden();

  await page.goto(`/w/${workspace.id}/projects`);
  await expect(page.getByText("No active projects")).toBeVisible();
});

test("stages are added, reordered, finished and deleted, and the progress follows", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await addClient(request, workspace.id, "Acme SRL");
  const created = await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website" } });
  expect(created.status()).toBe(201);
  const project = await created.json();
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/projects/${project.id}`);
  const stages = page.getByRole("region", { name: "Stages" });
  await expect(stages.getByText("No stages yet. Break the project")).toBeVisible();

  for (const name of ["Design", "Development", "Launch"]) {
    await stages.getByLabel("Stage name").fill(name);
    await stages.getByRole("button", { name: "Add stage" }).click();
    await expect(stages.getByRole("listitem").filter({ hasText: name })).toBeVisible();
  }
  await expect(stages.getByText("0 of 3 done")).toBeVisible();
  const order = stages.getByRole("listitem").getByRole("paragraph").filter({ hasText: /^(Design|Development|Launch)$/ });
  await expect(order).toHaveText(["Design", "Development", "Launch"]);

  // The first stage cannot go up and the last cannot go down; moving Launch up puts it before Development.
  await expect(stages.getByRole("button", { name: "Move Design up" })).toBeDisabled();
  await expect(stages.getByRole("button", { name: "Move Launch down" })).toBeDisabled();
  await stages.getByRole("button", { name: "Move Launch up" }).click();
  await expect(order).toHaveText(["Design", "Launch", "Development"]);

  await stages.getByRole("button", { name: "Edit Design" }).click();
  await stages.getByLabel("Status").selectOption("Done");
  await stages.getByRole("button", { name: "Save stage" }).click();
  await expect(stages.getByText("1 of 3 done")).toBeVisible();
  await expect(stages.getByRole("listitem").filter({ hasText: "Design" })).toContainText("Done");

  await stages.getByRole("button", { name: "Delete Launch" }).click();
  await page.getByRole("button", { name: "Keep stage" }).click();
  await expect(order).toHaveText(["Design", "Launch", "Development"]);
  await stages.getByRole("button", { name: "Delete Launch" }).click();
  await page.getByRole("button", { name: "Delete stage" }).click();
  await expect(order).toHaveText(["Design", "Development"]);
  await expect(stages.getByText("1 of 2 done")).toBeVisible();

  await page.goto(`/w/${workspace.id}/projects`);
  await expect(page.getByRole("row", { name: /Website/ })).toContainText("1 of 2 done");

  await page.goto(`/w/${workspace.id}/projects/${project.id}`);
  const editor = await openEditor(page);
  await editor.getByRole("button", { name: "Archive project" }).click();
  await expect(page.getByText("This project is archived")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(stages.getByRole("listitem")).toHaveCount(2);
  await expect(stages.getByRole("button", { name: "Add stage" })).toBeHidden();
  await expect(stages.getByRole("button", { name: "Edit Design" })).toBeHidden();
});

test("deliverables are added as links, shared, edited, archived and deleted", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await addClient(request, workspace.id, "Acme SRL");
  const created = await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website" } });
  const project = await created.json();
  await request.post(`/api/workspaces/${workspace.id}/projects/${project.id}/stages`, { data: { name: "Design" } });
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/projects/${project.id}`);
  await openTab(page, "Deliverables");
  const deliverables = page.getByRole("region", { name: "Deliverables" });
  await expect(deliverables.getByText("No deliverables yet.")).toBeVisible();

  // The API only accepts https links; its message reaches the form.
  await deliverables.getByLabel("Deliverable title").fill("Homepage mockup");
  await deliverables.getByLabel("Deliverable link").fill("http://figma.example/file");
  await deliverables.getByRole("button", { name: "Add deliverable" }).click();
  await expect(alertWith(page, "The link must be a valid https address.")).toBeVisible();

  await deliverables.getByLabel("Deliverable link").fill("https://figma.example/file");
  await deliverables.getByLabel("Deliverable stage").selectOption({ label: "Design" });
  await deliverables.getByRole("button", { name: "Add deliverable" }).click();
  const item = deliverables.getByRole("listitem").filter({ hasText: "Homepage mockup" });
  await expect(item).toContainText("Draft");
  await expect(item).toContainText("Stage: Design");
  await expect(item.getByRole("link", { name: "Homepage mockup" })).toHaveAttribute("href", "https://figma.example/file");
  await expect(deliverables.getByLabel("Deliverable title")).toHaveValue("");

  // A shared deliverable cannot be deleted; it goes back to draft first.
  await item.getByRole("button", { name: "Share Homepage mockup" }).click();
  await expect(item).toContainText("Shared");
  await expect(item.getByRole("button", { name: "Delete Homepage mockup" })).toBeHidden();
  await item.getByRole("button", { name: "Unshare Homepage mockup" }).click();
  await expect(item).toContainText("Draft");

  await item.getByRole("button", { name: "Edit Homepage mockup" }).click();
  await deliverables.getByLabel("Title", { exact: true }).fill("Final mockup");
  await deliverables.getByLabel("Stage", { exact: true }).selectOption("");
  await deliverables.getByRole("button", { name: "Save deliverable" }).click();
  const renamed = deliverables.getByRole("listitem").filter({ hasText: "Final mockup" });
  await expect(renamed).toBeVisible();
  await expect(renamed).not.toContainText("Stage:");

  await renamed.getByRole("button", { name: "Share Final mockup" }).click();
  await expect(renamed).toContainText("Shared");
  await renamed.getByRole("button", { name: "Archive Final mockup" }).click();
  await expect(deliverables.getByRole("heading", { name: "Archived" })).toBeVisible();
  const archived = deliverables.getByRole("listitem").filter({ hasText: "Final mockup" });
  await expect(archived).toContainText("Archived");
  await expect(archived.getByRole("button", { name: "Edit Final mockup" })).toBeHidden();
  await archived.getByRole("button", { name: "Restore Final mockup" }).click();
  await expect(deliverables.getByRole("heading", { name: "Archived" })).toBeHidden();
  await expect(renamed).toContainText("Draft"); // back to draft, never shared again by itself

  await renamed.getByRole("button", { name: "Delete Final mockup" }).click();
  await page.getByRole("button", { name: "Keep deliverable" }).click();
  await expect(renamed).toBeVisible();
  await renamed.getByRole("button", { name: "Delete Final mockup" }).click();
  await page.getByRole("button", { name: "Delete deliverable" }).click();
  await expect(deliverables.getByText("No deliverables yet.")).toBeVisible();

  await deliverables.getByLabel("Deliverable title").fill("Contract");
  await deliverables.getByLabel("Deliverable link").fill("https://docs.example/contract");
  await deliverables.getByRole("button", { name: "Add deliverable" }).click();
  await expect(deliverables.getByRole("listitem").filter({ hasText: "Contract" })).toBeVisible();
  const editor = await openEditor(page);
  await editor.getByRole("button", { name: "Archive project" }).click();
  await expect(page.getByText("This project is archived")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(deliverables.getByRole("listitem")).toHaveCount(1);
  await expect(deliverables.getByRole("button", { name: "Add deliverable" })).toBeHidden();
  await expect(deliverables.getByRole("button", { name: "Share Contract" })).toBeHidden();
});

test("approvals are requested and withdrawn, and an open request freezes the deliverable", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await addClient(request, workspace.id, "Acme SRL");
  const created = await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website" } });
  const project = await created.json();
  const deliverablesUrl = `/api/workspaces/${workspace.id}/projects/${project.id}/deliverables`;
  const deliverable = await (await request.post(deliverablesUrl, { data: { title: "Mockup", url: "https://figma.example/file" } })).json();
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/projects/${project.id}`);
  await openTab(page, "Deliverables");
  const deliverables = page.getByRole("region", { name: "Deliverables" });
  const item = deliverables.getByRole("listitem").filter({ hasText: "Mockup" });

  // Only a shared deliverable can be put in front of the client.
  await expect(item.getByRole("button", { name: "Request approval for Mockup" })).toBeHidden();
  await item.getByRole("button", { name: "Share Mockup" }).click();
  await expect(item).toContainText("Shared");

  await item.getByRole("button", { name: "Request approval for Mockup" }).click();
  await deliverables.getByLabel("Note for your client").fill("Check the logo on mobile.");
  await deliverables.getByRole("button", { name: "Send request" }).click();
  await expect(item).toContainText("Awaiting approval");
  await expect(item).toContainText("You asked: “Check the logo on mobile.”");
  await expect(deliverables.getByLabel("Note for your client")).toBeHidden();

  // An open request freezes the details and cannot be asked twice; the history keeps the round.
  await expect(item.getByRole("button", { name: "Edit Mockup" })).toBeHidden();
  await expect(item.getByRole("button", { name: "Request approval for Mockup" })).toBeHidden();
  await item.getByText("Approval history (1)").click();
  await expect(item.getByRole("listitem")).toHaveCount(1);

  await item.getByRole("button", { name: "Withdraw approval request for Mockup" }).click();
  await expect(item.getByRole("button", { name: "Edit Mockup" })).toBeVisible();
  await expect(item.getByRole("button", { name: "Withdraw approval request for Mockup" })).toBeHidden();
  await expect(item.getByRole("listitem")).toContainText("Withdrawn");

  // A new round, then unsharing takes it back by itself.
  await item.getByRole("button", { name: "Request approval for Mockup" }).click();
  await deliverables.getByRole("button", { name: "Send request" }).click();
  await expect(item).toContainText("Approval history (2)");
  await expect(item).toContainText("Awaiting approval");
  await item.getByRole("button", { name: "Unshare Mockup" }).click();
  await expect(item).toContainText("Draft");
  await expect(item).not.toContainText("Awaiting approval");

  // With an approval history it can no longer be deleted: it is archived instead.
  await expect(item.getByRole("button", { name: "Delete Mockup" })).toBeHidden();
  await expect(item.getByRole("button", { name: "Archive Mockup" })).toBeVisible();

  const stored = await (await request.get(`${deliverablesUrl}/${deliverable.id}`)).json();
  expect(stored.approvals.map((round: { status: string }) => round.status)).toEqual(["Withdrawn", "Withdrawn"]);
  expect(stored.approvalStatus).toBe("None");
});

test("updates are written, shared, edited, archived and deleted", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await addClient(request, workspace.id, "Acme SRL");
  const created = await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website" } });
  const project = await created.json();
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/projects/${project.id}`);
  const updates = page.getByRole("region", { name: "Updates" });
  await expect(updates.getByText("No updates yet.")).toBeVisible();

  await updates.getByLabel("Update title").fill("Design approved");
  await updates.getByLabel("What's happening now?").fill("Layouts are signed off.\nDevelopment starts Monday.");
  await updates.getByRole("button", { name: "Add update" }).click();
  const item = updates.getByRole("listitem").filter({ hasText: "Design approved" });
  await expect(item).toContainText("Draft");
  await expect(item).toContainText("Development starts Monday.");
  await expect(updates.getByLabel("Update title")).toHaveValue("");

  // A shared update cannot be deleted; it goes back to draft first.
  await item.getByRole("button", { name: "Share Design approved" }).click();
  await expect(item).toContainText("Shared");
  await expect(item).toContainText(/Shared \d{1,2} \w{3} \d{4}/);
  await expect(item.getByRole("button", { name: "Delete Design approved" })).toBeHidden();
  await item.getByRole("button", { name: "Unshare Design approved" }).click();
  await expect(item).toContainText("Draft");
  await expect(item).not.toContainText(/Shared \d/);

  await item.getByRole("button", { name: "Edit Design approved" }).click();
  await updates.getByLabel("Title", { exact: true }).fill("Build started");
  await updates.getByLabel("Details", { exact: true }).fill("");
  await updates.getByRole("button", { name: "Save update" }).click();
  const renamed = updates.getByRole("listitem").filter({ hasText: "Build started" });
  await expect(renamed).toBeVisible();
  await expect(renamed).not.toContainText("Development starts Monday.");

  await renamed.getByRole("button", { name: "Share Build started" }).click();
  await expect(renamed).toContainText("Shared");
  await renamed.getByRole("button", { name: "Archive Build started" }).click();
  await expect(updates.getByRole("heading", { name: "Archived" })).toBeVisible();
  const archived = updates.getByRole("listitem").filter({ hasText: "Build started" });
  await expect(archived).toContainText("Archived");
  await expect(archived.getByRole("button", { name: "Edit Build started" })).toBeHidden();
  await archived.getByRole("button", { name: "Restore Build started" }).click();
  await expect(updates.getByRole("heading", { name: "Archived" })).toBeHidden();
  await expect(renamed).toContainText("Draft"); // back to draft, never shared again by itself

  await renamed.getByRole("button", { name: "Delete Build started" }).click();
  await page.getByRole("button", { name: "Keep update" }).click();
  await expect(renamed).toBeVisible();
  await renamed.getByRole("button", { name: "Delete Build started" }).click();
  await page.getByRole("button", { name: "Delete update" }).click();
  await expect(updates.getByText("No updates yet.")).toBeVisible();

  await updates.getByLabel("Update title").fill("Kickoff done");
  await updates.getByRole("button", { name: "Add update" }).click();
  await expect(updates.getByRole("listitem").filter({ hasText: "Kickoff done" })).toBeVisible();
  const editor = await openEditor(page);
  await editor.getByRole("button", { name: "Archive project" }).click();
  await expect(page.getByText("This project is archived")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(updates.getByRole("listitem")).toHaveCount(1);
  await expect(updates.getByRole("button", { name: "Add update" })).toBeHidden();
  await expect(updates.getByRole("button", { name: "Share Kickoff done" })).toBeHidden();
});

test("a workspace with no clients is asked to add one before starting a project", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/projects`);
  await page.getByRole("button", { name: "New project" }).click();

  const dialog = page.getByRole("dialog", { name: "New project" });
  await expect(dialog.getByText("A project belongs to a client.")).toBeVisible();
  await dialog.getByRole("link", { name: "Go to clients" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/clients`);
});

test("a project page keeps its sections in tabs, the open tab is in the address, and the overview shows deadline and approvals", async ({
  page,
  request,
}) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await addClient(request, workspace.id, "Acme SRL");
  const dueDate = new Date(Date.now() + 10 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
  const project = await (
    await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website", dueDate } })
  ).json();
  const undated = await (await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Brand" } })).json();
  const deliverablesUrl = `/api/workspaces/${workspace.id}/projects/${project.id}/deliverables`;
  const deliverable = await (await request.post(deliverablesUrl, { data: { title: "Mockup", url: "https://figma.example/file" } })).json();
  expect((await request.post(`${deliverablesUrl}/${deliverable.id}/share`)).ok()).toBe(true);
  expect((await request.post(`${deliverablesUrl}/${deliverable.id}/approvals`, { data: { note: "Look it over." } })).ok()).toBe(true);
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  // It opens on the overview, with the stages, the time left and what waits on the client.
  const projectUrl = `/w/${workspace.id}/projects/${project.id}`;
  await page.goto(projectUrl);
  await expect(page.getByRole("tab", { name: "Overview" })).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("region", { name: "Stages" })).toBeVisible();
  await expect(page.getByText(/^(9|10) days left$/)).toBeVisible();
  await expect(page.getByText("1 waiting for the client")).toBeVisible();
  await expect(page.getByRole("region", { name: "Updates" })).toBeVisible();
  await expect(page.getByRole("tab", { name: "Deliverables & Files 1" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Deliverables" })).toHaveCount(0);

  // A button in the overview opens another tab, and the address says which.
  await page.getByRole("button", { name: "Open deliverables" }).click();
  await expect(page).toHaveURL(`${projectUrl}?tab=deliverables`);
  await expect(page.getByRole("region", { name: "Deliverables" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Files" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Stages" })).toHaveCount(0);

  await page.reload();
  await expect(page.getByRole("tab", { name: "Deliverables & Files" })).toHaveAttribute("aria-selected", "true");
  await page.goto(`${projectUrl}?tab=nonsense`);
  await expect(page.getByRole("tab", { name: "Overview" })).toHaveAttribute("aria-selected", "true");

  await openTab(page, "Deliverables & Files");
  await expect(page).toHaveURL(`${projectUrl}?tab=deliverables`);
  await openTab(page, "Overview");
  await expect(page).toHaveURL(projectUrl);

  // The project is edited from the button next to its name, in a panel over the page.
  const editor = await openEditor(page);
  await expect(editor.getByLabel("Name", { exact: true })).toHaveValue("Website");
  await page.keyboard.press("Escape");
  await expect(editor).toBeHidden();

  // Without dates the card says so and leads to where they are set.
  await page.goto(`/w/${workspace.id}/projects/${undated.id}`);
  await expect(page.getByText("No dates yet.")).toBeVisible();
  await page.getByRole("button", { name: "Set dates" }).click();
  await expect(page.getByRole("dialog", { name: "Edit project" }).getByLabel("Due date")).toBeVisible();
});
