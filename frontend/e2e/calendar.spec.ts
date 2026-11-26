import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { buildEvents, parseMonth, type CalendarSource } from "@/lib/calendar/calendar";
import type { Stage, StageStatus } from "@/lib/projects/types";
import { calendarTitle, createAccount, createWorkspace, expectOnCalendarDay, mainNav, showDeadlines, signInAt } from "./helpers";
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
  startDate: null,
  dueDate: null,
  stages: [],
  clientName: null,
  ...overrides,
});

test("a bad month falls back to the current one", () => {
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

test("a stage covers the days it has on the timeline: after the previous stage, up to its own end", () => {
  const events = buildEvents(
    [
      source({
        name: "Website",
        startDate: "2026-11-02",
        dueDate: "2026-11-30",
        stages: [
          { ...stage("2026-11-20"), name: "Build" },
          { ...stage("2026-11-10"), name: "Design" },
          { ...stage("2026-11-12", "Done"), name: "Review" },
          { ...stage(null), name: "Launch" },
        ],
      }),
      // No start date, so the stages have no span: each is its own day.
      source({ name: "Logo", dueDate: "2026-11-25", stages: [{ ...stage("2026-11-18"), name: "Sketch" }] }),
    ],
    "2026-11-16",
  );

  // Design: from the start to the 10th. Review (done) held the 12th, so Build runs from the 13th to the 20th. Launch has no date.
  expect(events.filter((event) => event.kind === "stage").map((event) => [event.title, event.date, event.endDate])).toEqual([
    ["Design", "2026-11-02", "2026-11-10"],
    ["Build", "2026-11-13", "2026-11-20"],
    ["Sketch", "2026-11-18", "2026-11-18"],
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
  startDate: string | null = null,
) {
  const url = `/api/workspaces/${workspaceId}/projects`;
  const created = await (await request.post(url, { data: { clientId, name } })).json();
  const updated = await request.put(`${url}/${created.id}`, { data: { name, description: null, startDate, dueDate, status } });
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
  // Research ends on the 10th, so Design runs from the 11th; as the last stage it reaches the project's end, the 31st.
  await addProject(
    request,
    workspace.id,
    client.id,
    "Website",
    "Active",
    `${FUTURE}-31`,
    [
      { name: "Design", dueDate: `${FUTURE}-20`, status: "InProgress" },
      { name: "Research", dueDate: `${FUTURE}-10`, status: "Done" },
    ],
    `${FUTURE}-05`,
  );
  await addProject(request, workspace.id, client.id, "Old job", "Completed", `${FUTURE}-15`);
  await addProject(request, workspace.id, client.id, "Logo", "Active", `${PAST}-10`);

  await signInAt(page, user, `/w/${workspace.id}`);
  await mainNav(page).getByRole("link", { name: "Calendar" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/calendar`);
  const thisMonth = new Date().toLocaleString("en-GB", { month: "long", year: "numeric" });
  await expect(calendarTitle(page)).toHaveText(thisMonth);

  // `?month=` is the month the calendar opens on.
  const main = page.getByRole("main");
  await page.goto(`/w/${workspace.id}/calendar?month=${FUTURE}`);
  await expect(calendarTitle(page)).toHaveText("January 2099");
  // Deadlines are off until asked for.
  await expect(main.getByRole("button", { name: /^Website/ })).toHaveCount(0);
  await showDeadlines(page);
  await expectOnCalendarDay(page, main.getByRole("button", { name: /^Website, All day/ }), `${FUTURE}-31`);
  await expectOnCalendarDay(page, main.getByRole("button", { name: /^Website: Design, All day/ }), `${FUTURE}-11`, `${FUTURE}-31`);
  // A finished stage and a completed project have nothing left to plan.
  await expect(main.getByRole("button", { name: /Research|Old job/ })).toHaveCount(0);

  await page.goto(`/w/${workspace.id}/calendar?month=${PAST}`);
  await showDeadlines(page);
  await expectOnCalendarDay(page, main.getByRole("button", { name: /^Logo \(overdue\)/ }), `${PAST}-10`);

  await page.goto(`/w/${workspace.id}/calendar?month=${FUTURE}`);
  await showDeadlines(page);
  await main.getByRole("button", { name: "Next", exact: true }).click();
  await expect(calendarTitle(page)).toHaveText("February 2099");
  await main.getByRole("button", { name: "Today", exact: true }).click();
  await expect(calendarTitle(page)).toHaveText(thisMonth);

  await page.goto(`/w/${workspace.id}/calendar?month=${FUTURE}`);
  await showDeadlines(page);
  await main.getByRole("button", { name: /^Website: Design, All day/ }).first().click();
  await expect(page.getByRole("heading", { level: 1, name: "Website" })).toBeVisible();

  // Not a month: the current one, not an error.
  await page.goto(`/w/${workspace.id}/calendar?month=nonsense`);
  await expect(calendarTitle(page)).toHaveText(thisMonth);
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
  await showDeadlines(page);
  await expectOnCalendarDay(page, main.getByRole("button", { name: /^Website, All day/ }), `${FUTURE}-31`);
  await expectOnCalendarDay(page, main.getByRole("button", { name: /^Website: Design, All day/ }), `${FUTURE}-20`);
  await expect(main.getByRole("button", { name: /Secret plan/ })).toHaveCount(0);

  await main.getByRole("button", { name: /^Website: Design, All day/ }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Website" })).toBeVisible();
});
