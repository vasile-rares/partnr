import { randomUUID } from "node:crypto";
import { expect, type APIRequestContext, type Locator, type Page } from "@playwright/test";

export const PASSWORD = "correct horse battery";

// Every test gets its own account, so tests are independent and can run in parallel against the same database.
export const newUser = () => ({ email: `e2e-${randomUUID()}@example.com`, fullName: "Ana Pop", password: PASSWORD });

// Creates an account without touching the browser session: the `request` fixture has its own cookie storage
// (unlike `page.request`), so the page stays signed out.
export async function createAccount(request: APIRequestContext) {
  const user = newUser();
  const response = await request.post("/api/auth/register", { data: user });
  expect(response.status()).toBe(201);
  return user;
}

export async function signIn(page: Page, email: string, password: string) {
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
}

// Signs the page in through the API and opens `path`: the same session the sign-in form gives (`page.request` shares the page's
// cookies), without rendering the form and the redirects after it. Only the tests of the sign-in itself use the form.
export async function signInAt(page: Page, user: { email: string; password: string }, path: string) {
  const response = await page.request.post("/api/auth/login", { data: { email: user.email, password: user.password } });
  expect(response.ok(), "sign-in through the API").toBe(true);
  await page.goto(path);
  await expect(page).toHaveURL(path);
}

// Next.js has its own (empty) role="alert" live region, so alerts are matched by their text.
export const alertWith = (page: Page, text: string) => page.getByRole("alert").filter({ hasText: text });

// Creates a workspace as the account last registered through this request context (it keeps the session cookie).
export async function createWorkspace(request: APIRequestContext, name: string): Promise<{ id: string; name: string }> {
  const response = await request.post("/api/workspaces", { data: { name } });
  expect(response.status()).toBe(201);
  return response.json();
}

// The sidebar's main menu: Home, Projects, Clients and the project shortcuts (Settings sits in the footer, outside it).
export const mainNav = (page: Page) => page.getByRole("navigation", { name: "Main" });

// The breadcrumb in the top bar of every page inside the app shell.
export const breadcrumb = (page: Page) => page.getByRole("navigation", { name: "breadcrumb" });

// The account menu at the bottom of the sidebar; its button carries the signed-in person's name and email.
export const accountMenu = (page: Page, email: string) => page.getByRole("button", { name: email });

// A project page keeps its sections in tabs; only the open one is on the page.
export const openTab = (page: Page, name: string) => page.getByRole("tab", { name }).click();

// The panel that edits a project (and archives it) opens from the button next to the project's name. It is modal:
// close it (Escape) before using anything behind it.
export async function openEditor(page: Page) {
  await page.getByRole("button", { name: "Edit project", exact: true }).click();
  return page.getByRole("dialog", { name: "Edit project" });
}

// "Add client" and "New project" open a dialog in the middle of the screen; it is modal, so what is behind it is out
// of reach until it closes.
export async function openAddClient(page: Page) {
  await page.getByRole("button", { name: "Add client", exact: true }).click();
  return page.getByRole("dialog", { name: "Add a client" });
}

// Same for a client: the panel with the contact form and the archive button.
export async function openClientEditor(page: Page) {
  await page.getByRole("button", { name: "Edit client", exact: true }).click();
  return page.getByRole("dialog", { name: "Edit client" });
}

// The Calendar page: its title ("January 2099"), and a check that an event covers exactly the days `from` to `to` ("YYYY-MM-DD", one day when
// `to` is left out) of the month grid. An event over several weeks is one element per week, so `events` may match several.
export const calendarTitle = (page: Page) => page.getByRole("main").locator('[data-slot="event-calendar-title"]');

// Project deadlines are off until asked for.
export const showDeadlines = (page: Page) => page.getByRole("main").getByRole("checkbox", { name: "Show deadlines" }).click();

export async function expectOnCalendarDay(page: Page, events: Locator, from: string, to = from) {
  const DAY = 86_400_000;
  const shift = (date: string, days: number) => new Date(Date.parse(`${date}T00:00:00Z`) + days * DAY).toISOString().slice(0, 10);

  await expect(events.first()).toBeVisible();
  const boxes = (await Promise.all((await events.all()).map((event) => event.boundingBox()))).filter((box) => box !== null);

  // True when an element of the event sits over the middle of the day's cell, null when the day is not on the grid.
  const covers = async (date: string) => {
    const [year, month, day] = date.split("-").map(Number);
    const monthName = new Date(Date.UTC(year, month - 1, 1)).toLocaleString("en-GB", { month: "long", timeZone: "UTC" });
    const cell = await page
      .getByRole("main")
      .getByRole("gridcell", { name: new RegExp(`, ${day} ${monthName} ${year}$`) })
      .boundingBox();
    if (!cell) {
      return null;
    }
    const x = cell.x + cell.width / 2;
    return boxes.some((box) => {
      const y = box.y + box.height / 2;
      return x > box.x && x < box.x + box.width && y > cell.y && y < cell.y + cell.height;
    });
  };

  for (let date = from; date <= to; date = shift(date, 1)) {
    expect(await covers(date), `${date} is covered`).toBe(true);
  }
  expect(await covers(shift(from, -1)), "the day before is not covered").not.toBe(true);
  expect(await covers(shift(to, 1)), "the day after is not covered").not.toBe(true);
}

// A day on an open calendar, found by the date it stands for as the browser writes it (the tests run in en-US).
export const calendarDay = (page: Page, isoDate: string) => {
  const [year, month, day] = isoDate.split("-").map(Number);
  return page.locator(`[data-slot="calendar"] [data-day="${month}/${day}/${year}"]`);
};

// Pages the open calendar until the day is on it, then picks it.
export async function pickDate(page: Page, isoDate: string) {
  const day = calendarDay(page, isoDate);
  for (const direction of ["next", "previous"]) {
    const paging = page.getByRole("button", { name: new RegExp(`${direction} month`, "i") });
    for (let step = 0; step < 12 && !(await day.isVisible()) && (await paging.isEnabled()); step++) {
      await paging.click();
    }
  }
  await day.click();
}
