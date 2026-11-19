# Messages (one conversation per project)

Details and reasons: `decisions.md`, "Mesaje".

## API

- `Message` and `ConversationRead`. One `MessageService` and one set of routes, `/api/workspaces/{id}/conversations`, for both sides: a member of the workspace and a client of it (resolved by `CallerAccess`). Each side has its own door inside the service (a member reaches every project of the workspace, a client only the ones shared with them), and both hand over to `Messaging/ConversationThreads`; the client's name is left out of what a client receives.
- A conversation exists only while the client can open the project (`Project.IsOpenToClient`, the portal's rule), otherwise 404.
- Plain text up to 4000 characters; the author edits or deletes within 10 minutes (403 for someone else, 409 after or once deleted; a deleted message loses its text and keeps its place).
- Read marks are per person, set from the id of the newest message shown and never moved back.
- A new, edited or deleted message and a read mark publish `ConversationChanged` (project id and the accounts that read the conversation, before the save, like every event); the Notifications service pushes `conversationChanged` with the project id over its hub ([notifications.md](notifications.md)).

## Web app

- `app/w/[workspaceId]/messages/` with a `[projectId]/` page below it (the list alone, and the list with a conversation open), for firm and client alike (the page opens the workspace with `getOpenWorkspace`, which also answers a client), on `MessagesView` (`src/components/messages/`: `ConversationList`, `ConversationThread`, `UnreadMessagesBadge` on the "Messages" menu item through `unreadFrom` in `nav.ts`), data in `src/lib/messages/` (`conversationsApi` names the API). The client's sidebar links to the same page.
- Live through `useConversationChanges` from `NotificationsProvider` (the same hub connection as the bell).
- Enter sends, Shift+Enter is a new line; the author's own message has Edit / Delete for 10 minutes.
- In e2e tests the thread is `getByRole("log")`, the box is the textbox "Write a message", and project names also appear in the sidebar, so look for the conversation's link inside `getByRole("main")`.
