# Messages (one conversation per project)

Details and reasons: `decisions.md`, "Mesaje".

## API

- `Message` and `ConversationRead`. The firm reaches them through `MessageService` (`/api/workspaces/{id}/conversations`, members) and the client through `PortalMessageService` (`/api/portal/workspaces/{id}/conversations`, the portal's door); both hand over to `Messaging/ConversationThreads`.
- A conversation exists only while the client can open the project (`ConversationThreads.IsOpen`, the portal's rule), otherwise 404.
- Plain text up to 4000 characters; the author edits or deletes within 10 minutes (403 for someone else, 409 after or once deleted; a deleted message loses its text and keeps its place).
- Read marks are per person, set from the id of the newest message shown and never moved back.
- Changes push `conversationChanged` with the project id over the notifications hub (`IConversationPublisher`).

## Web app

- `app/w/[workspaceId]/messages/` and `app/portal/[workspaceId]/messages/`, each with a `[projectId]/` page below it (the list alone, and the list with a conversation open), both on `MessagesView` (`src/components/messages/`: `ConversationList`, `ConversationThread`, `UnreadMessagesBadge` on the "Messages" menu item through `unreadFrom` in `nav.ts`), data in `src/lib/messages/` (`firmConversationsApi` / `portalConversationsApi` name each side's API).
- Live through `useConversationChanges` from `NotificationsProvider` (the same hub connection as the bell).
- Enter sends, Shift+Enter is a new line; the author's own message has Edit / Delete for 10 minutes.
- In e2e tests the thread is `getByRole("log")`, the box is the textbox "Write a message", and project names also appear in the sidebar, so look for the conversation's link inside `getByRole("main")`.
