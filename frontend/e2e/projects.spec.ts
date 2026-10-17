import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { createAccount, createWorkspace, signIn } from "./helpers";

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
  await page.getByRole("link", { name: "New project" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/projects/new?clientId=${client.id}`);
  await expect(page.getByLabel("Client", { exact: true })).toHaveValue(client.id);

  await page.getByLabel("Name", { exact: true }).fill("Website");
  await page.getByLabel("Due date").fill("2026-12-15");
  await page.getByRole("button", { name: "Create project" }).click();

  await expect(page.getByRole("heading", { level: 1, name: "Website" })).toBeVisible();
  await expect(page.getByText("Not visible in their portal yet")).toBeVisible();

  await page.getByLabel("Status").selectOption("Active");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page.getByRole("status")).toHaveText("Changes saved.");
  await expect(page.getByText("Not visible in their portal yet")).toBeHidden();

  await page.getByRole("banner").getByRole("link", { name: "Projects" }).click();
  const row = page.getByRole("row", { name: /Website/ });
  await expect(row).toContainText("Acme SRL");
  await expect(row).toContainText("Active");
  await expect(row).toContainText("15 Dec 2026");

  await row.getByRole("link", { name: "Website" }).click();
  await page.getByRole("button", { name: "Archive project" }).click();
  await expect(page.getByText("This project is archived")).toBeVisible();
  await expect(page.getByLabel("Name", { exact: true })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Save changes" })).toBeHidden();

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
  await page.getByRole("button", { name: "Archive project" }).click();
  await expect(page.getByText("This project is archived")).toBeVisible();
  await expect(stages.getByRole("listitem")).toHaveCount(2);
  await expect(stages.getByRole("button", { name: "Add stage" })).toBeHidden();
  await expect(stages.getByRole("button", { name: "Edit Design" })).toBeHidden();
});

test("a workspace with no clients is asked to add one before starting a project", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/projects/new`);

  await expect(page.getByText("A project belongs to a client.")).toBeVisible();
  await expect(page.getByRole("link", { name: "Add client" })).toBeVisible();
});
