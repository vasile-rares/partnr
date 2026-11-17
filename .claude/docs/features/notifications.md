# Notifications (stored, emailed, inbox, live push)

Details and reasons: `decisions.md`, "Notificări".

## API

- Notifications are rows of `Notification` (`INotificationRepository`), written by the services through `INotificationWriter` **before** their `SaveChanges`, so a notification exists exactly when its change does.
- Three events: a request for approval tells the client (the account linked to the project's card, and only if they can open the project), the client's answer tells the member who asked, an accepted invitation tells the member who invited (a member who left is skipped).
- Withdrawing a request, unsharing or archiving its deliverable marks its notification obsolete (`ApprovalRequestWithdrawnAsync`): it leaves the inbox and the email, if it has not left yet, is cancelled.
- **Inbox**: `INotificationService` behind `/api/notifications` (`GET`, `GET unread-count`, `POST {id}/read`, `POST read-all`): the account's own, across workspaces, newest 30; someone else's, an obsolete one and an unknown id are all 404.
- **Live push**: after a save that worked, `InboxPushInterceptor` (an EF `SaveChangesInterceptor`) finds the accounts whose notifications were added, read or made obsolete and tells them through `IInboxPublisher`; the Api's `SignalRInboxPublisher` sends `inboxChanged` (no data, the browser asks the REST routes) over `NotificationsHub` at `/api/hubs/notifications` (under `/api`, so the web app's rewrite carries the WebSocket), to the account by id (`Clients.User`, no groups). The email worker's own changes push nothing. One API instance only: more would need a backplane. A failed push is logged and never fails the request. **Needs ASP.NET Core 10.0.13 or newer in production** (GHSA-334f-vvp4-7g6p, a SignalR denial of service fixed in 10.0.13).
- **Email**: sent by `NotificationEmailWorker` through `INotificationEmailService.SendDueAsync`: it claims due rows with one `UPDATE … RETURNING … FOR UPDATE SKIP LOCKED` that also pushes their next attempt 5 minutes out, retries up to 5 times (1, 5, 15, 60 minutes), then marks the row `Failed`. The `Notifications` section turns the worker off (`EmailWorkerEnabled`, off in the integration tests, which call the service themselves) or changes its pace (`EmailPollSeconds`, 5). The text is built once in `NotificationContent` (title and link only, never the user's own message).

## Web app

- The `NotificationsBell` sits in the top bar of `Page.tsx` (`src/components/notifications/`): a popover with the latest 30, a click marks one read and opens it, "Mark all as read"; its button is named "Notifications, N unread".
- The data and the SignalR connection live in `NotificationsProvider`, mounted in `AppShell` because a layout outlives page navigation: one WebSocket-only connection per tab and area, the server only says `inboxChanged` and the REST routes answer, no polling; the state reloads whenever the connection (re)starts. Messages use the same connection ([messages.md](messages.md)).
- In e2e tests a bell with no number also means "not loaded yet", so wait for a number.
