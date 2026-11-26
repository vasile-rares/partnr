import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { buildEvents } from "@/lib/calendar/calendar";
import { durationLabel, durationOf, momentsOf, splitMeetings, whenLabel } from "@/lib/meetings/time";
import type { Meeting } from "@/lib/meetings/types";
import { calendarTitle, createAccount, createWorkspace, expectOnCalendarDay, mainNav, pickDate, signInAt } from "./helpers";
import { invitationTokenIn, waitForEmails } from "./mailpit";

// The browser is in Bucharest, so what is stored in UTC has to be shown (and placed on the calendar) in that zone.
test.use({ timezoneId: "Europe/Bucharest" });

// The API runs with a stand-in for Google (Google__Fake): a meeting is "sent" within a few seconds and gets a Meet link.
const SYNC_TIMEOUT = 30_000;

// ---------------------------------------------------------------- pure rules, no browser

const meeting = (startsAtUtc: string, minutes: number, status: Meeting["status"] = "Scheduled"): Pick<Meeting, "startsAtUtc" | "endsAtUtc" | "status"> => ({
  startsAtUtc,
  endsAtUtc: new Date(Date.parse(startsAtUtc) + minutes * 60_000).toISOString(),
  status,
});

test("Upcoming is what is scheduled and not over; Past is the rest, latest first", () => {
  const now = "2026-11-16T12:00:00.000Z";
  const ended = meeting("2026-11-16T10:00:00.000Z", 60);
  const running = meeting("2026-11-16T11:30:00.000Z", 60);
  const later = meeting("2026-11-20T09:00:00.000Z", 30);
  const sooner = meeting("2026-11-17T09:00:00.000Z", 30);
  const cancelled = meeting("2026-11-18T09:00:00.000Z", 30, "Cancelled");
  const older = meeting("2026-11-01T09:00:00.000Z", 30);

  const { upcoming, past } = splitMeetings([later, ended, cancelled, sooner, older, running], now);

  expect(upcoming).toEqual([running, sooner, later]);
  expect(past).toEqual([cancelled, ended, older]);
});

test("a chosen date and time become two moments, in the reader's own zone", () => {
  const moments = momentsOf("2099-07-15", "10:30", 45)!;
  const start = new Date(moments.startsAt);

  expect([start.getFullYear(), start.getMonth(), start.getDate(), start.getHours(), start.getMinutes()]).toEqual([2099, 6, 15, 10, 30]);
  expect(durationOf({ startsAtUtc: moments.startsAt, endsAtUtc: moments.endsAt })).toBe(45);

  for (const [date, time, minutes] of [["", "10:30", 30], ["2099-07-15", "", 30], ["2099-07-15", "10:30", 0], ["nonsense", "10:30", 30]] as const) {
    expect(momentsOf(date, time, minutes)).toBeNull();
  }
});

test("a meeting reads as a day, a time range and a zone", () => {
  const at = { startsAtUtc: "2099-07-15T07:30:00.000Z", endsAtUtc: "2099-07-15T08:15:00.000Z" };

  expect(whenLabel(at, "UTC")).toBe("Wed 15 Jul · 07:30–08:15 UTC");
  expect(whenLabel(at, "Europe/Bucharest")).toMatch(/^Wed 15 Jul · 10:30–11:15 (EEST|GMT\+3)$/);
  // Over midnight the end names its own day.
  expect(whenLabel({ startsAtUtc: "2099-07-15T23:00:00.000Z", endsAtUtc: "2099-07-16T01:00:00.000Z" }, "UTC")).toBe("Wed 15 Jul · 23:00–Thu 16 Jul 01:00 UTC");
  expect([durationLabel(15), durationLabel(60), durationLabel(90), durationLabel(120)]).toEqual(["15 min", "1 h", "1 h 30 min", "2 h"]);
});

test("the calendar holds every meeting, the ones called off marked, by the hour before the deadlines", () => {
  const events = buildEvents(
    [{ id: "p", name: "Website", status: "Active", startDate: null, dueDate: "2026-11-20", stages: [], clientName: null }],
    "2026-11-16",
    [
      { id: "late", title: "Afternoon", startsAtUtc: "2026-11-20T15:00:00.000Z", endsAtUtc: "2026-11-20T15:30:00.000Z", status: "Scheduled", clientName: "Elena" },
      { id: "off", title: "Called off", startsAtUtc: "2026-11-20T10:00:00.000Z", endsAtUtc: "2026-11-20T10:30:00.000Z", status: "Cancelled", clientName: "Elena" },
      { id: "early", title: "Morning", startsAtUtc: "2026-11-20T08:00:00.000Z", endsAtUtc: "2026-11-20T08:30:00.000Z", status: "Scheduled", clientName: "Elena" },
    ],
  );

  expect(events.map((event) => [event.kind, event.title, event.cancelled])).toEqual([
    ["meeting", "Morning", false],
    ["meeting", "Called off", true],
    ["meeting", "Afternoon", false],
    ["project", "Website", false],
  ]);
  expect(events[0]).toMatchObject({ date: "2026-11-20", projectId: null, late: false });
});

// ---------------------------------------------------------------- the freelancer's side

async function signedInFreelancer(page: Page, request: APIRequestContext) {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  await signInAt(page, user, `/w/${workspace.id}`);

  return { user, workspace };
}

async function addClient(request: APIRequestContext, workspaceId: string, name: string, email = `client-${randomUUID()}@example.com`) {
  const response = await request.post(`/api/workspaces/${workspaceId}/clients`, { data: { name, email } });
  expect(response.status()).toBe(201);

  return (await response.json()) as { id: string };
}

async function addProject(request: APIRequestContext, workspaceId: string, clientId: string, name: string) {
  const created = await (await request.post(`/api/workspaces/${workspaceId}/projects`, { data: { clientId, name } })).json();
  const updated = await request.put(`/api/workspaces/${workspaceId}/projects/${created.id}`, {
    data: { name, description: null, startDate: null, dueDate: null, status: "Active" },
  });
  expect(updated.ok()).toBe(true);

  return created as { id: string };
}

// Google's side of "Connect Google Calendar", without the clicks: the API's stand-in sends the browser straight back.
async function connectGoogle(request: APIRequestContext) {
  const { url } = await (await request.post("/api/integrations/google/connect")).json();
  const state = new URL(url).searchParams.get("state")!;
  const back = await request.get(`/api/integrations/google/callback?code=fake-ok&state=${encodeURIComponent(state)}`, { maxRedirects: 0 });
  expect(back.status()).toBe(302);
}

type Scheduled = { id: string; syncStatus: string; meetUrl: string | null };

async function schedule(
  request: APIRequestContext,
  workspaceId: string,
  clientId: string,
  title: string,
  startsAt: Date,
  minutes = 30,
  projectId: string | null = null,
) {
  const response = await request.post(`/api/workspaces/${workspaceId}/meetings`, {
    data: { clientId, projectId, title, startsAt: startsAt.toISOString(), endsAt: new Date(startsAt.getTime() + minutes * 60_000).toISOString() },
  });
  expect(response.status()).toBe(201);

  return (await response.json()) as Scheduled;
}

async function waitUntilSent(request: APIRequestContext, workspaceId: string, id: string) {
  await expect
    .poll(async () => ((await (await request.get(`/api/workspaces/${workspaceId}/meetings/${id}`)).json()) as Scheduled).syncStatus, { timeout: SYNC_TIMEOUT })
    .toBe("Synced");
}

const inDays = (days: number, hourUtc = 9) => {
  const date = new Date(Date.now() + days * 86_400_000);
  date.setUTCHours(hourUtc, 0, 0, 0);

  return date;
};

/** "YYYY-MM-DD" of a moment in Bucharest, the zone the browser is in. */
const bucharestDay = (date: Date) => date.toLocaleDateString("sv-SE", { timeZone: "Europe/Bucharest" });

// A meeting on the month grid is a button named "<title>, <time>" (a called off one: "<title> (cancelled), <time>").
const escaped = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const meetingButton = (page: Page, title: string) => page.getByRole("main").getByRole("button", { name: new RegExp(`^${escaped(title)}( \\(cancelled\\))?, `) });

// The calendar opens on the current month; pages on when the day is in the next one.
async function showMonthOf(page: Page, isoDate: string) {
  const label = new Date(`${isoDate}T12:00:00`).toLocaleString("en-GB", { month: "long", year: "numeric" });
  if ((await calendarTitle(page).textContent()) !== label) {
    await page.getByRole("main").getByRole("button", { name: "Next", exact: true }).click();
  }
  await expect(calendarTitle(page)).toHaveText(label);
}

// Opens a meeting from the month that has its day; its dialog is named after it.
async function openMeeting(page: Page, title: string, isoDate: string) {
  await showMonthOf(page, isoDate);
  await meetingButton(page, title).click();

  return page.getByRole("dialog", { name: title });
}

test("without Google connected, the Calendar says so and its button connects Google", async ({ page, request }) => {
  const { workspace } = await signedInFreelancer(page, request);
  await addClient(request, workspace.id, "Acme SRL");

  await mainNav(page).getByRole("link", { name: "Calendar" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/calendar`);
  await expect(page.getByRole("heading", { level: 1, name: "Calendar" })).toBeVisible();

  await expect(page.getByRole("alert").filter({ hasText: "Connect your Google Calendar to schedule meetings" })).toBeVisible();
  await expect(page.getByRole("button", { name: "New meeting" })).toHaveCount(0);
  await expect(calendarTitle(page)).toBeVisible();

  // The button starts the connection itself (the stand-in for Google sends the browser straight back) and the way back ends on the calendar.
  await page.getByRole("main").getByRole("button", { name: "Connect Google to schedule" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/calendar?google=connected`);
  await expect(page.getByRole("status").filter({ hasText: "Google Calendar connected" })).toBeVisible();
  await expect(page.getByRole("button", { name: "New meeting" })).toBeVisible();
  await expect(page.getByRole("alert").filter({ hasText: "Connect your Google Calendar" })).toHaveCount(0);
});

test("a meeting is scheduled in the dialog, sent to Google, and shows in the browser's own time", async ({ page, request }) => {
  const { workspace } = await signedInFreelancer(page, request);
  const acme = await addClient(request, workspace.id, "Acme SRL");
  await addProject(request, workspace.id, acme.id, "Website");
  await connectGoogle(page.request);

  await page.goto(`/w/${workspace.id}/calendar`);
  await expect(page.getByRole("alert").filter({ hasText: "Connect your Google Calendar" })).toHaveCount(0);
  await page.getByRole("button", { name: "New meeting" }).click();
  const dialog = page.getByRole("dialog", { name: "New meeting" });

  const day = bucharestDay(inDays(3));
  await dialog.getByLabel("Client").selectOption({ label: "Acme SRL" });
  await dialog.getByLabel("Project").selectOption({ label: "Website" });
  await dialog.getByLabel("Title").fill("Kick-off");
  await dialog.getByLabel("Date", { exact: true }).click();
  await pickDate(page, day);
  await dialog.getByLabel("Start time").fill("10:30");
  await dialog.getByLabel("Length").selectOption("45");
  await dialog.getByLabel("Notes").fill("Bring the brief");
  await expect(dialog).toContainText("Ends at 11:15. Times are in your time zone (Europe/Bucharest).");
  await dialog.getByRole("button", { name: "Schedule meeting" }).click();

  await expect(dialog).toHaveCount(0);
  const details = await openMeeting(page, "Kick-off", day);
  await expect(details).toContainText("10:30–11:15");
  await expect(details).toContainText("Acme SRL");
  await expect(details).toContainText("Website");
  await expect(details).toContainText("45 min");
  await expect(details).toContainText("Bring the brief");

  // The page asks again while Google has not answered, so the Meet link turns up by itself.
  const join = details.getByRole("link", { name: /Join Kick-off/ });
  await expect(join).toBeVisible({ timeout: SYNC_TIMEOUT });
  await expect(join).toHaveAttribute("href", /^https:\/\/meet\.google\.com\//);
  await expect(join).toHaveAttribute("target", "_blank");
  await expect(details.getByText(/Sending to Google/)).toHaveCount(0);
});

test("a meeting can be changed and called off, and the cancelled one stays on the calendar, marked", async ({ page, request }) => {
  const { workspace } = await signedInFreelancer(page, request);
  const acme = await addClient(request, workspace.id, "Acme SRL");
  await connectGoogle(page.request);
  const when = inDays(3);
  const created = await schedule(request, workspace.id, acme.id, "Kick-off", when);
  await waitUntilSent(request, workspace.id, created.id);

  await page.goto(`/w/${workspace.id}/calendar`);
  const day = bucharestDay(when);
  const details = await openMeeting(page, "Kick-off", day);
  await details.getByRole("button", { name: "Edit Kick-off" }).click();
  const dialog = page.getByRole("dialog", { name: "Edit meeting" });
  await expect(dialog).toContainText("Acme SRL");
  await expect(dialog.getByLabel("Title")).toHaveValue("Kick-off");
  await dialog.getByLabel("Title").fill("Kick-off, second try");
  await dialog.getByLabel("Start time").fill("14:00");
  await dialog.getByRole("button", { name: "Save meeting" }).click();
  await expect(dialog).toHaveCount(0);

  const edited = page.getByRole("dialog", { name: "Kick-off, second try" });
  await expect(edited).toContainText("14:00–14:30");
  await expect(edited.getByRole("link", { name: /Join/ })).toBeVisible({ timeout: SYNC_TIMEOUT });

  await edited.getByRole("button", { name: "Cancel Kick-off, second try" }).click();
  const confirm = page.getByRole("alertdialog");
  await expect(confirm).toContainText("Google tells Acme SRL that “Kick-off, second try” is cancelled");
  await confirm.getByRole("button", { name: "Keep the meeting" }).click();
  await expect(edited.getByRole("button", { name: "Edit Kick-off, second try" })).toBeVisible();

  await edited.getByRole("button", { name: "Cancel Kick-off, second try" }).click();
  await confirm.getByRole("button", { name: "Cancel meeting" }).click();

  // History: it stays, marked, with nothing left to do.
  await expect(edited).toContainText("Cancelled");
  await expect(edited.getByRole("button", { name: /Edit|Cancel/ })).toHaveCount(0);
  await expect(edited.getByRole("link", { name: /Join/ })).toHaveCount(0);
  await page.keyboard.press("Escape");
  await expect(page.getByRole("main").getByRole("button", { name: /^Kick-off, second try \(cancelled\), / })).toBeVisible();
});

test("a start in the past is refused with a message, and nothing is saved", async ({ page, request }) => {
  const { workspace } = await signedInFreelancer(page, request);
  await addClient(request, workspace.id, "Acme SRL");
  await connectGoogle(page.request);

  await page.goto(`/w/${workspace.id}/calendar`);
  await page.getByRole("button", { name: "New meeting" }).click();
  const dialog = page.getByRole("dialog", { name: "New meeting" });
  await dialog.getByLabel("Client").selectOption({ label: "Acme SRL" });
  await dialog.getByLabel("Title").fill("Too late");

  await dialog.getByRole("button", { name: "Schedule meeting" }).click();
  await expect(dialog.getByRole("alert").filter({ hasText: "Pick a date and a start time." })).toBeVisible();

  await dialog.getByLabel("Date", { exact: true }).click();
  await pickDate(page, bucharestDay(new Date()));
  await dialog.getByLabel("Start time").fill("00:00");
  await dialog.getByRole("button", { name: "Schedule meeting" }).click();

  await expect(dialog.getByRole("alert").filter({ hasText: "A meeting has to start in the future." })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(meetingButton(page, "Too late")).toHaveCount(0);
});

test("a meeting Google cannot be told about keeps its mark, and has no Join link", async ({ page, request }) => {
  const { workspace } = await signedInFreelancer(page, request);
  const acme = await addClient(request, workspace.id, "Acme SRL");
  await connectGoogle(page.request);
  const when = inDays(3);
  await schedule(request, workspace.id, acme.id, "[google-down] Strategy", when);

  await page.goto(`/w/${workspace.id}/calendar`);
  const day = bucharestDay(when);
  const details = await openMeeting(page, "[google-down] Strategy", day);
  await expect(details.getByText("Sending to Google…")).toBeVisible();

  // One round of the background sync (it runs every 5 seconds) fails for this title and puts the next try a minute away.
  await page.waitForTimeout(7_000);
  await page.reload();
  const again = await openMeeting(page, "[google-down] Strategy", day);
  await expect(again.getByText("Sending to Google…")).toBeVisible();
  await expect(again.getByRole("link", { name: /Join/ })).toHaveCount(0);
});

test("a client's page schedules with the client already chosen, and lists what is coming up", async ({ page, request }) => {
  const { workspace } = await signedInFreelancer(page, request);
  const acme = await addClient(request, workspace.id, "Acme SRL");
  await addClient(request, workspace.id, "Other SRL");
  await connectGoogle(page.request);

  await page.goto(`/w/${workspace.id}/clients/${acme.id}`);
  await expect(page.getByText("No meetings coming up with this client.")).toBeVisible();
  await page.getByRole("button", { name: "New meeting" }).click();
  const dialog = page.getByRole("dialog", { name: "New meeting" });
  await expect(dialog.getByLabel("Client")).toHaveValue(acme.id);
  await dialog.getByLabel("Title").fill("Intro call");
  const day = bucharestDay(inDays(3));
  await dialog.getByLabel("Date", { exact: true }).click();
  await pickDate(page, day);
  await dialog.getByRole("button", { name: "Schedule meeting" }).click();

  await expect(dialog).toHaveCount(0);
  const row = page.getByRole("main").getByRole("listitem").filter({ hasText: "Intro call" });
  await expect(row).toBeVisible();
  await page.getByRole("link", { name: "Open the calendar" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/calendar`);
  const details = await openMeeting(page, "Intro call", day);
  await expect(details).toContainText("Acme SRL");
});

test("meetings are shown in the reader's zone, on the calendar day that is theirs", async ({ page, request }) => {
  const { workspace } = await signedInFreelancer(page, request);
  const acme = await addClient(request, workspace.id, "Acme SRL");
  await connectGoogle(page.request);
  // 07:30 UTC is 10:30 in Bucharest in July; 22:30 UTC is already 01:30 of the next day there.
  await schedule(request, workspace.id, acme.id, "Morning call", new Date("2099-07-15T07:30:00Z"), 45);
  await schedule(request, workspace.id, acme.id, "Late call", new Date("2099-07-15T22:30:00Z"), 30);

  await page.goto(`/w/${workspace.id}/calendar?month=2099-07`);
  await expectOnCalendarDay(page, page.getByRole("main").getByRole("button", { name: /^Morning call, 10:30/ }), "2099-07-15");
  await expectOnCalendarDay(page, page.getByRole("main").getByRole("button", { name: /^Late call, 01:30/ }), "2099-07-16");

  await page.getByRole("main").getByRole("button", { name: /^Morning call, 10:30/ }).click();
  await expect(page.getByRole("dialog", { name: "Morning call" })).toContainText(/Wed 15 Jul · 10:30–11:15 (EEST|GMT\+3)/);
  await page.keyboard.press("Escape");
  await page.getByRole("main").getByRole("button", { name: /^Late call, 01:30/ }).click();
  await expect(page.getByRole("dialog", { name: "Late call" })).toContainText(/Thu 16 Jul · 01:30–02:00/);
});

// ---------------------------------------------------------------- the client's side

test("the client sees its meetings in the portal: next on Home, and on the calendar with Join and history", async ({ page, request }) => {
  await createAccount(request);
  const workspace = await createWorkspace(request, "Studio Pop");
  await connectGoogle(request);
  const clientEmail = `client-${randomUUID()}@example.com`;
  const elena = await addClient(request, workspace.id, "Elena Ionescu", clientEmail);
  const website = await addProject(request, workspace.id, elena.id, "Website");
  const secret = await (await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: elena.id, name: "Secret plan" } })).json();
  const soon = await schedule(request, workspace.id, elena.id, "Design review", new Date("2099-07-15T07:30:00Z"), 45, website.id);
  const hidden = await schedule(request, workspace.id, elena.id, "About the secret", new Date("2099-07-16T07:30:00Z"), 30, secret.id);
  const off = await schedule(request, workspace.id, elena.id, "Called off", new Date("2099-07-17T07:30:00Z"), 30);
  for (const sent of [soon, hidden, off]) {
    await waitUntilSent(request, workspace.id, sent.id);
  }
  await request.post(`/api/workspaces/${workspace.id}/meetings/${off.id}/cancel`);
  expect((await request.post(`/api/workspaces/${workspace.id}/clients/${elena.id}/invitation`)).ok()).toBe(true);
  const token = invitationTokenIn((await waitForEmails(clientEmail, 1))[0]);

  await page.goto(`/invite/${token}`);
  await page.getByLabel("Full name").fill("Elena Popescu");
  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByRole("button", { name: "Create account and accept" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  const next = page.getByRole("region", { name: "Next meeting" });
  await expect(next).toContainText("Design review");
  await expect(next).toContainText(/10:30–11:15/);
  await expect(next.getByRole("link", { name: /Join Design review/ })).toHaveAttribute("href", /^https:\/\/meet\.google\.com\//);

  await mainNav(page).getByRole("link", { name: "Calendar" }).click();
  await expect(page).toHaveURL(`/w/${workspace.id}/calendar`);
  await expect(mainNav(page).getByRole("link", { name: "Meetings" })).toHaveCount(0);

  await page.goto(`/w/${workspace.id}/calendar?month=2099-07`);
  const main = page.getByRole("main");
  await expectOnCalendarDay(page, main.getByRole("button", { name: /^Design review, 10:30/ }), "2099-07-15");

  await main.getByRole("button", { name: /^Design review, 10:30/ }).click();
  const review = page.getByRole("dialog", { name: "Design review" });
  await expect(review).toContainText("Website");
  await expect(review.getByRole("link", { name: /Join/ })).toBeVisible();
  await expect(review.getByRole("button", { name: /Edit|Cancel/ })).toHaveCount(0);
  await page.keyboard.press("Escape");

  // A project the client cannot open is not named.
  await main.getByRole("button", { name: /^About the secret, / }).click();
  await expect(page.getByRole("dialog", { name: "About the secret" })).not.toContainText("Secret plan");
  await page.keyboard.press("Escape");

  // The call off one stays, marked, with nothing to join.
  await main.getByRole("button", { name: /^Called off \(cancelled\), / }).click();
  const history = page.getByRole("dialog", { name: "Called off" });
  await expect(history).toContainText("Cancelled");
  await expect(history.getByRole("link", { name: /Join/ })).toHaveCount(0);
});
