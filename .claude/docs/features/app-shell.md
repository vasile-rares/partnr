# The app shell (sidebar, menus, theme)

Plan of the redesign: [ui-redesign.md](../ui-redesign.md). Where `/` sends someone and the workspace pages: [workspaces.md](workspaces.md).

## Web app

- **App shell** (`src/components/shell/`): every signed-in page sits in a sidebar. `AppShell` (server) loads the user, the workspaces they work in and the ones they are a client of, and renders `AppSidebar` (client: it needs `usePathname`, so it only receives serializable data, `ShellContext` in `types.ts`). Two layouts feed it: `app/w/[workspaceId]/layout.tsx` (`kind: "workspace"`, with up to 5 Active projects; for a client, who opens the same address, the client's menu instead, `kind: "client"`, with the projects shared with them) and `app/(shell)/layout.tsx` (`kind: "none"`: `/account`, `/workspaces`, `/workspaces/new`).
- The menus are in `nav.ts`: an item without `href` is a future feature and shows disabled with a "Soon" badge, so turning one on means giving it an `href`. `ContextSwitcher` (workspaces / "Client of") is the sidebar header, `NavUser` (Account, Theme, Sign out) the footer.
- `Page.tsx`: top bar with the sidebar toggle and breadcrumb and, on the right, the `NotificationsBell` (see [notifications.md](notifications.md)), then the content column; `narrow` for forms.
- Dark is the default theme (`theme-provider.tsx`).
- In e2e tests the sidebar's links are under `mainNav(page)` and the breadcrumb under `breadcrumb(page)` (`e2e/helpers.ts`); project names also appear in the sidebar, so look for links inside `getByRole("main")`.
