# Dashboard and calendar

Neither has an endpoint of its own: both are built from the lists the API already gives.

## Dashboard

- `app/w/[workspaceId]/page.tsx`, blocks in `src/components/dashboard/`, rules in `src/lib/dashboard/deadlines.ts`.
- "Needs your attention" (projects with `approvals` to act on or wait for, changes requested first), "Coming up" (deadlines of projects and unfinished stages within 14 days, plus late ones), "Active projects", "Waiting on clients" (invitation sent or expired); a workspace with no projects sees `GettingStarted` instead.
- Built from `getProjects` and `getClients`.

## Calendar

- `app/w/[workspaceId]/calendar/` and `app/portal/[workspaceId]/calendar/`, both on `CalendarView` in `src/components/calendar/`, rules in `src/lib/calendar/calendar.ts`, pure and tested without a browser in `e2e/calendar.spec.ts` and `e2e/meetings.spec.ts`.
- A month grid (Monday first; a day list on narrow screens) of project deadlines, unfinished stages and **meetings that were not called off**, built from `getProjects` / `getPortalProject` / `getMeetings` / `getPortalMeetings`.
- The month is in the address (`?month=2026-11`), changed with plain links.
- `CalendarView` is a client component only because a meeting belongs to the day it is on **for the reader**: the server puts it on its UTC day and the browser moves it to the local one once it has taken over (`useInBrowser`); a deadline is a day with no zone.
- A meeting pill shows a video icon and the hour and links to the Meetings page.
- In e2e tests a day is `[data-date="YYYY-MM-DD"]` inside `getByRole("main")` and an event link is named "Project deadline: X", "Stage of X: Y" or "Meeting: 10:30 Title" (with "(overdue)" when late); use far-off months (2099-01, 2099-07, 2020-03) so the result does not depend on today.
