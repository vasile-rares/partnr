import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { alertWith, calendarDay, createAccount, createWorkspace, mainNav, openEditor, openTab, pickDate, signIn } from "./helpers";

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

test("stages are added, reordered, edited and deleted from the bar, and the progress follows", async ({ page, request }) => {
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
  const timeline = page.getByRole("region", { name: "Timeline" });
  const segment = (name: string) => timeline.getByRole("button", { name: new RegExp(`^${name},`) });
  const order = () =>
    timeline
      .getByRole("button", { name: /^(Design|Development|Launch),/ })
      .evaluateAll((items) => items.map((item) => item.getAttribute("aria-label")!.split(",")[0]));
  await expect(timeline.getByText("Break the project into steps")).toBeVisible();

  // Stages are made in a dialog: the first one from the empty bar, the next ones from the + at its end.
  const adding = page.getByRole("dialog", { name: "Add a stage" });
  for (const [position, name] of ["Design", "Development", "Launch"].entries()) {
    await timeline.getByRole("button", { name: position === 0 ? "Add the first stage" : "Add stage" }).click();
    await adding.getByLabel("Stage name").fill(name);
    await adding.getByRole("button", { name: "Add stage" }).click();
    await expect(adding).toBeHidden();
    await expect(segment(name)).toBeVisible();
  }
  await expect(timeline.getByText("0 of 3 done")).toBeVisible();
  await expect.poll(order).toEqual(["Design", "Development", "Launch"]);

  // The bar draws the stages by their dates, so giving Launch an earlier date than Development puts it before it. The
  // menu under a segment edits everything in place; a date is picked on a calendar and saved at once.
  const inDays = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);
  const menu = page.locator('[data-slot="popover-content"]').filter({ has: page.getByLabel("Stage name") });
  await segment("Development").click();
  await menu.getByLabel("Due date").click();
  await pickDate(page, inDays(20));
  await expect(menu.getByRole("status")).toHaveText("Saved");
  await page.keyboard.press("Escape");
  await segment("Launch").click();
  await menu.getByLabel("Due date").click();
  await pickDate(page, inDays(10));
  await expect.poll(order).toEqual(["Design", "Launch", "Development"]);
  await page.keyboard.press("Escape");

  // Status is one click, the name and the description save when they are left, and a name cannot be emptied.
  await segment("Design").click();
  await menu.getByRole("button", { name: "Done" }).click();
  await expect(timeline.getByText("1 of 3 done")).toBeVisible();
  await expect(segment("Design")).toHaveAccessibleName(/^Design, Done/);
  await menu.getByLabel("Description").fill("Logo and layouts.");
  await menu.getByLabel("Stage name").fill("");
  await menu.getByLabel("Stage name").blur();
  await expect(menu.getByText("A stage needs a name.")).toBeVisible();
  await expect(menu.getByLabel("Stage name")).toHaveValue("Design");
  await menu.getByLabel("Stage name").fill("Visual design");
  await menu.getByLabel("Stage name").press("Enter");
  await expect(timeline.getByRole("button", { name: /^Visual design, Done/ })).toBeVisible();
  await page.keyboard.press("Escape");
  await timeline.getByRole("button", { name: /^Visual design,/ }).click();
  await expect(menu.getByLabel("Description")).toHaveValue("Logo and layouts.");
  await menu.getByLabel("Stage name").fill("Design");
  await menu.getByLabel("Stage name").press("Enter");
  await expect(segment("Design")).toBeVisible();
  await page.keyboard.press("Escape");

  await segment("Launch").click();
  await page.getByRole("button", { name: "Delete Launch" }).click();
  await page.getByRole("button", { name: "Keep stage" }).click();
  await expect.poll(order).toEqual(["Design", "Launch", "Development"]);
  await expect(page.getByRole("button", { name: "Delete Launch" })).toBeVisible();
  await page.getByRole("button", { name: "Delete Launch" }).click();
  await page.getByRole("button", { name: "Delete stage" }).click();
  await expect.poll(order).toEqual(["Design", "Development"]);
  await expect(timeline.getByText("1 of 2 done")).toBeVisible();

  await page.goto(`/w/${workspace.id}/projects`);
  await expect(page.getByRole("row", { name: /Website/ })).toContainText("1 of 2 done");

  // An archived project's bar can be read, but not changed.
  await page.goto(`/w/${workspace.id}/projects/${project.id}`);
  const editor = await openEditor(page);
  await editor.getByRole("button", { name: "Archive project" }).click();
  await expect(page.getByText("This project is archived")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(timeline.getByRole("button", { name: "Add stage" })).toBeHidden();
  await segment("Design").click();
  await expect(page.locator('[data-slot="popover-content"]')).toContainText("Design");
  await expect(page.getByLabel("Stage name")).toBeHidden();
});

test("a stage's end is dragged, or nudged with the keyboard, to change its due date", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await addClient(request, workspace.id, "Acme SRL");
  const inDays = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);
  const projectsUrl = `/api/workspaces/${workspace.id}/projects`;
  const project = await (
    await request.post(projectsUrl, { data: { clientId: client.id, name: "Website", startDate: inDays(-5), dueDate: inDays(35) } })
  ).json();
  for (const [name, due] of [["Design", 5], ["Build", 15], ["Launch", 35]] as const) {
    expect((await request.post(`${projectsUrl}/${project.id}/stages`, { data: { name, dueDate: inDays(due) } })).ok()).toBe(true);
  }
  const dueOf = async (name: string) =>
    (await (await request.get(`${projectsUrl}/${project.id}`)).json()).stages.find((stage: { name: string }) => stage.name === name).dueDate;
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/projects/${project.id}`);
  const timeline = page.getByRole("region", { name: "Timeline" });
  // The last stage ends where the project does, so only the edges between stages can be moved.
  await expect(timeline.getByRole("slider")).toHaveCount(2);

  // Arrow keys move it a day at a time, and the change is saved a moment after the last key.
  const design = timeline.getByRole("slider", { name: "Due date of Design" });
  await design.focus();
  for (let press = 0; press < 3; press++) {
    await page.keyboard.press("ArrowRight");
  }
  await expect(timeline.getByRole("status")).toContainText("+3 days");
  await expect.poll(() => dueOf("Design")).toBe(inDays(8));
  await expect(timeline.getByRole("status")).toBeHidden();

  // Dragging does the same with the pointer, and a plain press without moving leaves the date alone.
  const build = timeline.getByRole("slider", { name: "Due date of Build" });
  await timeline.hover();
  const box = (await build.boundingBox())!;
  const [x, y] = [box.x + box.width / 2, box.y + box.height / 2];
  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.mouse.up();
  expect(await dueOf("Build")).toBe(inDays(15));

  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.mouse.move(x - 80, y, { steps: 8 });
  await expect(timeline.getByRole("status")).toContainText("days)");
  await page.mouse.up();
  // Dates are ISO strings, so they compare as text.
  await expect.poll(async () => (await dueOf("Build")) < inDays(15)).toBe(true);
  expect((await dueOf("Build")) > (await dueOf("Design"))).toBe(true);

  // An archived project's dates stay as they are.
  expect((await request.post(`${projectsUrl}/${project.id}/archive`)).ok()).toBe(true);
  await page.reload();
  await expect(timeline.getByRole("slider")).toHaveCount(0);
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

  // Adding is asked for: nothing of the form is on the page, and the dialog opens from the header or the empty message.
  const adding = page.getByRole("dialog", { name: "Add a deliverable" });
  await expect(deliverables.getByLabel("Deliverable title")).toHaveCount(0);
  await deliverables.getByRole("button", { name: "Add the first deliverable" }).click();

  // The API only accepts https links; its message reaches the form.
  await adding.getByLabel("Deliverable title").fill("Homepage mockup");
  await adding.getByLabel("Deliverable link").fill("http://figma.example/file");
  await adding.getByRole("button", { name: "Add deliverable" }).click();
  await expect(alertWith(page, "The link must be a valid https address.")).toBeVisible();

  await adding.getByLabel("Deliverable link").fill("https://figma.example/file");
  await adding.getByLabel("Deliverable stage").selectOption({ label: "Design" });
  await adding.getByRole("button", { name: "Add deliverable" }).click();
  const item = deliverables.getByRole("listitem").filter({ hasText: "Homepage mockup" });
  await expect(item).toContainText("Draft");
  await expect(item).toContainText("Stage: Design");
  await expect(item.getByRole("link", { name: "Homepage mockup" })).toHaveAttribute("href", "https://figma.example/file");
  await expect(adding).toBeHidden();

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

  await deliverables.getByRole("button", { name: "Add the first deliverable" }).click();
  await adding.getByLabel("Deliverable title").fill("Contract");
  await adding.getByLabel("Deliverable link").fill("https://docs.example/contract");
  await adding.getByRole("button", { name: "Add deliverable" }).click();
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

  // Adding is asked for: nothing of the form is on the page, and the dialog opens from the header button.
  const adding = page.getByRole("dialog", { name: "Add an update" });
  await expect(updates.getByLabel("Update title")).toHaveCount(0);
  await updates.getByRole("button", { name: "Add update" }).click();
  await adding.getByLabel("Update title").fill("Design approved");
  await adding.getByLabel("What's happening now?").fill("Layouts are signed off.\nDevelopment starts Monday.");
  await adding.getByRole("button", { name: "Add update" }).click();
  const item = updates.getByRole("listitem").filter({ hasText: "Design approved" });
  await expect(item).toContainText("Draft");
  await expect(item).toContainText("Development starts Monday.");
  await expect(adding).toBeHidden();

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

  await updates.getByRole("button", { name: "Add the first update" }).click();
  await adding.getByLabel("Update title").fill("Kickoff done");
  await adding.getByRole("button", { name: "Add update" }).click();
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
  await expect(page.getByRole("region", { name: "Timeline" })).toBeVisible();
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
  await expect(page.getByRole("region", { name: "Updates" })).toHaveCount(0);

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

test("the project bar lays the stages along the dates, and a stage's status is changed from its segment", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await addClient(request, workspace.id, "Acme SRL");
  const inDays = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);
  const projectsUrl = `/api/workspaces/${workspace.id}/projects`;
  const project = await (
    await request.post(projectsUrl, { data: { clientId: client.id, name: "Website", startDate: inDays(-5), dueDate: inDays(25) } })
  ).json();
  for (const [name, due] of [["Design", 5], ["Build", 15], ["Launch", 25]] as const) {
    const added = await request.post(`${projectsUrl}/${project.id}/stages`, { data: { name, dueDate: inDays(due) } });
    expect(added.ok()).toBe(true);
  }
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/projects/${project.id}`);
  const timeline = page.getByRole("region", { name: "Timeline" });
  await expect(timeline.getByText("0 of 3 done")).toBeVisible();
  await expect(timeline.getByText(/^2[45] days left$/)).toBeVisible();
  await expect(timeline.getByText("Today", { exact: true })).toBeVisible();
  await expect(timeline.getByRole("button", { name: /, (To do|In progress|Done)/ })).toHaveCount(3);
  await expect(timeline.getByRole("button", { name: "Add stage" })).toBeVisible();

  // A segment opens the stage; its status buttons write the change and the bar follows.
  await timeline.getByRole("button", { name: /^Design, To do/ }).click();
  const statuses = page.getByRole("group", { name: "Status of Design" });
  await expect(statuses.getByRole("button", { name: "To do" })).toHaveAttribute("aria-pressed", "true");
  await statuses.getByRole("button", { name: "Done" }).click();
  await expect(timeline.getByText("1 of 3 done")).toBeVisible();
  await expect(timeline.getByRole("button", { name: /^Design, Done/ })).toBeVisible();
  await page.keyboard.press("Escape");

  // An archived project's stages can be read, but not changed.
  expect((await request.post(`${projectsUrl}/${project.id}/archive`)).ok()).toBe(true);
  await page.reload();
  await timeline.getByRole("button", { name: /^Build, To do/ }).click();
  await expect(page.locator('[data-slot="popover-content"]')).toContainText("Build");
  await expect(page.getByRole("group", { name: "Status of Build" })).toHaveCount(0);
});

test("a stage's date is picked on a calendar where days outside the project cannot be selected", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await addClient(request, workspace.id, "Acme SRL");
  const projectsUrl = `/api/workspaces/${workspace.id}/projects`;
  const project = await (
    await request.post(projectsUrl, { data: { clientId: client.id, name: "Website", startDate: "2040-03-10", dueDate: "2040-05-20" } })
  ).json();
  expect((await request.post(`${projectsUrl}/${project.id}/stages`, { data: { name: "Design", dueDate: "2040-04-15" } })).ok()).toBe(true);
  const stagesOf = async () => (await (await request.get(`${projectsUrl}/${project.id}`)).json()).stages;
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/projects/${project.id}`);
  const timeline = page.getByRole("region", { name: "Timeline" });
  const menu = page.locator('[data-slot="popover-content"]').filter({ has: page.getByLabel("Stage name") });
  const previous = page.getByRole("button", { name: /previous month/i });
  const next = page.getByRole("button", { name: /next month/i });

  // The calendar opens on the stage's month, stops at the project's first and last months, and greys out what is outside.
  await timeline.getByRole("button", { name: /^Design,/ }).click();
  await menu.getByLabel("Due date").click();
  await expect(calendarDay(page, "2040-04-15")).toBeVisible();
  await previous.click();
  await expect(calendarDay(page, "2040-03-09")).toBeDisabled();
  await expect(calendarDay(page, "2040-03-10")).toBeEnabled();
  await expect(previous).toBeDisabled();
  await next.click();
  await next.click();
  await expect(calendarDay(page, "2040-05-20")).toBeEnabled();
  await expect(calendarDay(page, "2040-05-21")).toBeDisabled();
  await expect(next).toBeDisabled();
  await calendarDay(page, "2040-05-21").click({ force: true });
  expect((await stagesOf())[0].dueDate).toBe("2040-04-15");

  await calendarDay(page, "2040-05-18").click();
  await expect.poll(async () => (await stagesOf())[0].dueDate).toBe("2040-05-18");
  await expect(menu.getByLabel("Due date")).toHaveText("18 May 2040");
  await page.keyboard.press("Escape");

  // A new stage has the same calendar, and a date can be taken back.
  await timeline.getByRole("button", { name: "Add stage" }).click();
  const adding = page.getByRole("dialog", { name: "Add a stage" });
  await adding.getByLabel("Stage name").fill("Launch");
  await adding.getByLabel("Stage due date").click();
  await expect(calendarDay(page, "2040-03-09")).toBeDisabled();
  await pickDate(page, "2040-05-20");
  await adding.getByRole("button", { name: "Add stage" }).click();
  await expect(adding).toBeHidden();
  await expect(timeline.getByRole("button", { name: /^Launch, To do, due 20 May 2040/ })).toBeVisible();

  // The API says the same if a request gets past the page, and the project's own dates cannot leave a stage outside them.
  const refused = await request.post(`${projectsUrl}/${project.id}/stages`, { data: { name: "Late", dueDate: "2040-05-21" } });
  expect(refused.status()).toBe(400);
  expect(await stagesOf()).toHaveLength(2);
  const editor = await openEditor(page);
  await editor.getByLabel("Due date").fill("2040-05-01");
  await editor.getByRole("button", { name: "Save changes" }).click();
  await expect(alertWith(page, "Change the stage first")).toBeVisible();
});
