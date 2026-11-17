import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { buildEvents, monthWeeks, parseMonth, shiftMonth, type CalendarSource } from "@/lib/calendar/calendar";
import type { Stage, StageStatus } from "@/lib/projects/types";
import { createAccount, createWorkspace, mainNav, signIn } from "./helpers";
import { invitationTokenIn, waitForEmails } from "./mailpit";

// Far-away months, so what is late and what is not does not depend on the day the tests run.
const FUTURE = "2099-01";
const PAST = "2020-03";

let counter = 0;
const stage = (dueDate: string | null, status: StageStatus = "ToDo"): Stage => ({
  id: `stage-${counter++}`,
  name: `Stage ${counter}`,
  description: null,
  dueDate,
  status,
});

const source = (overrides: Partial<CalendarSource>): CalendarSource => ({
  id: `project-${counter++}`,
  name: "Website",
  status: "Active",
  dueDate: null,
  stages: [],
  clientName: null,
  ...overrides,
});

test("a month is drawn as whole weeks starting on Monday", () => {
  const november = monthWeeks("2026-11");
  expect(november).toHaveLength(6);
  expect(november[0][0]).toBe("2026-10-26");
  expect(november[0][6]).toBe("2026-11-01");
  expect(november.at(-1)?.[6]).toBe("2026-12-06");

  // February 2027 starts on a Monday and ends on a Sunday: exactly four weeks.
  expect(monthWeeks("2027-02")).toHaveLength(4);
});

test("months shift across years, and a bad month falls back", () => {
  expect(shiftMonth("2026-12", 1)).toBe("2027-01");
  expect(shiftMonth("2026-01", -1)).toBe("2025-12");

  expect(parseMonth("2026-05", "2026-11")).toBe("2026-05");
  for (const bad of [undefined, "", "2026-13", "2026-5", "abc", "1999-01", ["2026-05"]]) {
    expect(parseMonth(bad, "2026-11")).toBe("2026-11");
  }
});

test("the calendar holds a project's end and its unfinished stages, nothing from a completed project", () => {
  const events = buildEvents(
    [
      source({ name: "Website", dueDate: "2026-11-20", stages: [stage("2026-11-10"), stage("2026-11-05", "Done"), stage(null)] }),
      source({ name: "Old job", status: "Completed", dueDate: "2026-11-01", stages: [stage("2026-11-02")] }),
      source({ name: "Logo", dueDate: "2026-11-02" }),
    ],
    "2026-11-16",
  );

  expect(events.map((event) => [event.date, event.kind, event.late])).toEqual([
    ["2026-11-02", "project", true],
    ["2026-11-10", "stage", true],
    ["2026-11-20", "project", false],
  ]);
});

async function addProject(
  request: APIRequestContext,
  workspaceId: string,
  clientId: string,
  name: string,
  status: string,
  dueDate: string,
  stages: { name: string; dueDate: string; status: StageStatus }[] = [],
) {
  const url = `/api/workspaces/${workspaceId}/projects`;
  const created = await (await request.post(url, { data: { clientId, name } })).json();
  const updated = await request.put(`${url}/${created.id}`, { data: { name, description: null, startDate: null, dueDate, status } });
  expect(updated.ok()).toBe(true);
  for (const { name: stageName, dueDate: stageDate, status: stageStatus } of stages) {
    const added = await (await request.post(`${url}/${created.id}/stages`, { data: { name: stageName } })).json();
    const saved = await request.put(`${url}/${created.id}/stages/${added.stages.at(-1).id}`, {
      data: { name: stageName, dueDate: stageDate, status: stageStatus },
    });
    expect(saved.ok()).toBe(true);
  }
  return created;
}

const addClient = async (request: APIRequestContext, workspaceId: string, name: string, email = `client-${randomUUID()}@example.com`) => {
  const response = await request.post(`/api/workspaces/${workspaceId}/clients`, { data: { name, email } });
  expect(response.status()).toBe(201);
  return response.json();
};

test("the freelancer sees deadlines on their day, pages through months and opens a project", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await addClient(request, workspace.id, "Acme SRL");
  await addProject(request, workspace.id, client.id, "Website", "Active", `${FUTURE}-31`, [
    { name: "Design", dueDate: `${FUTURE}-20`, status: "InProgress" },
    { name: "Research", dueDate: `${FUTURE}-10`, status: "Done" },
  ]);
  await addProject(request, workspace.id, client.id, "Old job", "Completed", `${FUTURE}-15`);
  await addProject(request, workspace.id, client.id, "Logo", "Active", `${PAST}-10`);

  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);
  await mainNav(page).getByRole("link", { name: "Calendar" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/calendar`);
  const thisMonth = new Date().toLocaleString("en-GB", { month: "long", year: "numeric", timeZone: "UTC" });
  await expect(page.getByRole("heading", { level: 2, name: thisMonth })).toBeVisible();

  const main = page.getByRole("main");
  await page.goto(`/w/${workspace.id}/calendar?month=${FUTURE}`);
  await expect(page.getByRole("heading", { level: 2, name: "January 2099" })).toBeVisible();
  await expect(main.locator('[data-date="2099-01-31"]').getByRole("link", { name: "Project deadline: Website" })).toBeVisible();
  await expect(main.locator('[data-date="2099-01-20"]').getByRole("link", { name: "Stage of Website: Design" })).toBeVisible();
  // A finished stage and a completed project have nothing left to plan.
  await expect(main.getByRole("link", { name: /Research|Old job/ })).toHaveCount(0);

  await page.goto(`/w/${workspace.id}/calendar?month=${PAST}`);
  await expect(main.locator('[data-date="2020-03-10"]').getByRole("link", { name: /Logo.*overdue/ })).toBeVisible();

  await page.goto(`/w/${workspace.id}/calendar?month=${FUTURE}`);
  await page.getByRole("link", { name: "Next month" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/calendar?month=2099-02`);
  await expect(page.getByRole("heading", { level: 2, name: "February 2099" })).toBeVisible();
  await page.getByRole("link", { name: "Today" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/calendar`);
  await expect(page.getByRole("heading", { level: 2, name: thisMonth })).toBeVisible();

  await page.goto(`/w/${workspace.id}/calendar?month=${FUTURE}`);
  await main.getByRole("link", { name: "Stage of Website: Design" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Website" })).toBeVisible();

  // Not a month: the current one, not an error.
  await page.goto(`/w/${workspace.id}/calendar?month=nonsense`);
  await expect(page.getByRole("heading", { level: 2, name: thisMonth })).toBeVisible();
});

test("the client sees the shared projects' deadlines, not the ones still in planning", async ({ page, request }) => {
  await createAccount(request);
  const workspace = await createWorkspace(request, "Studio Pop");
  const clientEmail = `client-${randomUUID()}@example.com`;
  const client = await addClient(request, workspace.id, "Elena Ionescu", clientEmail);
  await addProject(request, workspace.id, client.id, "Website", "Active", `${FUTURE}-31`, [
    { name: "Design", dueDate: `${FUTURE}-20`, status: "InProgress" },
  ]);
  await addProject(request, workspace.id, client.id, "Secret plan", "Planning", `${FUTURE}-25`);
  expect((await request.post(`/api/workspaces/${workspace.id}/clients/${client.id}/invitation`)).ok()).toBe(true);
  const token = invitationTokenIn((await waitForEmails(clientEmail, 1))[0]);

  await page.goto(`/invite/${token}`);
  await page.getByLabel("Full name").fill("Elena Popescu");
  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByRole("button", { name: "Create account and accept" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await mainNav(page).getByRole("link", { name: "Calendar" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/calendar`);

  await page.goto(`/w/${workspace.id}/calendar?month=${FUTURE}`);
  const main = page.getByRole("main");
  await expect(main.locator('[data-date="2099-01-31"]').getByRole("link", { name: "Project deadline: Website" })).toBeVisible();
  await expect(main.locator('[data-date="2099-01-20"]').getByRole("link", { name: "Stage of Website: Design" })).toBeVisible();
  await expect(main.getByRole("link", { name: /Secret plan/ })).toHaveCount(0);

  await main.getByRole("link", { name: "Stage of Website: Design" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Website" })).toBeVisible();
});
