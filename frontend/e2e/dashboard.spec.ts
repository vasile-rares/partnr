import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { createAccount, createWorkspace, signIn } from "./helpers";

// UTC like the page, so "in 3 days" means the same here and there.
const inDays = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);

async function addClient(request: APIRequestContext, workspaceId: string, name: string, invite = false): Promise<{ id: string }> {
  const response = await request.post(`/api/workspaces/${workspaceId}/clients`, {
    data: { name, email: `client-${randomUUID()}@example.com` },
  });
  expect(response.status()).toBe(201);
  const client = await response.json();
  if (invite) {
    expect((await request.post(`/api/workspaces/${workspaceId}/clients/${client.id}/invitation`)).ok()).toBe(true);
  }
  return client;
}

async function addProject(
  request: APIRequestContext,
  workspaceId: string,
  clientId: string,
  name: string,
  status: string,
  dueDate: string | null,
  stage?: { name: string; dueDate: string },
) {
  const url = `/api/workspaces/${workspaceId}/projects`;
  const created = await (await request.post(url, { data: { clientId, name } })).json();
  const updated = await request.put(`${url}/${created.id}`, { data: { name, description: null, startDate: null, dueDate, status } });
  expect(updated.ok()).toBe(true);
  if (stage) {
    const added = await (await request.post(`${url}/${created.id}/stages`, { data: { name: stage.name } })).json();
    const saved = await request.put(`${url}/${created.id}/stages/${added.stages.at(-1).id}`, {
      data: { name: stage.name, dueDate: stage.dueDate, status: "InProgress" },
    });
    expect(saved.ok()).toBe(true);
  }
  return created;
}

test("a new workspace first asks for a client, then for a project", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await expect(page.getByText("Add your first client")).toBeVisible();
  await expect(page.getByRole("button", { name: "Add client" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Coming up" })).toHaveCount(0);

  await addClient(request, workspace.id, "Acme SRL");
  await page.reload();
  await expect(page.getByText("Create a project", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "New project" })).toBeVisible();
});

test("the dashboard lists what is due, what is running and who has not joined yet", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const acme = await addClient(request, workspace.id, "Acme SRL", true);
  const notInvited = await addClient(request, workspace.id, "Not Invited SRL");

  await addProject(request, workspace.id, acme.id, "Website", "Active", inDays(40), { name: "Design", dueDate: inDays(3) });
  await addProject(request, workspace.id, acme.id, "Logo", "Active", inDays(-2));
  await addProject(request, workspace.id, acme.id, "Brochure", "OnHold", null);
  await addProject(request, workspace.id, notInvited.id, "Draft", "Planning", inDays(5));
  await addProject(request, workspace.id, notInvited.id, "Old job", "Completed", inDays(-10));

  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);
  await expect(page.getByRole("heading", { level: 1, name: "Studio" })).toBeVisible();

  // Late work first, then what is due soon; a far-off project date and a finished project stay out.
  const comingUp = page.getByRole("region", { name: "Coming up" }).getByRole("listitem");
  await expect(comingUp).toHaveCount(3);
  await expect(comingUp.nth(0)).toContainText("Logo");
  await expect(comingUp.nth(0)).toContainText("2 days overdue");
  await expect(comingUp.nth(1)).toContainText("Design");
  await expect(comingUp.nth(1)).toContainText("Stage of Website");
  await expect(comingUp.nth(1)).toContainText("In 3 days");
  await expect(comingUp.nth(2)).toContainText("Draft");

  const running = page.getByRole("region", { name: "Active projects" }).getByRole("listitem");
  await expect(running).toHaveCount(3);
  await expect(running.filter({ hasText: "Brochure" })).toContainText("On hold");
  await expect(running.filter({ hasText: "Draft" })).toHaveCount(0);
  await expect(running.filter({ hasText: "Old job" })).toHaveCount(0);
  await running.getByRole("link", { name: "Website" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Website" })).toBeVisible();

  await page.goto(`/w/${workspace.id}`);
  const waiting = page.getByRole("region", { name: "Waiting on clients" }).getByRole("listitem");
  await expect(waiting).toHaveCount(1);
  await expect(waiting).toContainText("Acme SRL");
  await expect(waiting).toContainText("Invited");
  await waiting.getByRole("link", { name: "Acme SRL" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Acme SRL" })).toBeVisible();
});
