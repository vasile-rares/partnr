# Development seed data

How to seed and reseed is in `backend/CLAUDE.md` ("Test data"). This file lists what the seed puts in the database. The data lives in `backend/src/Services/Workspaces/Partnr.Workspaces.Infrastructure/Persistence/Seeding/DevelopmentDataSeeder.cs`; add to it when a new feature needs something to click through, and update this list.

- Accounts (password `demo-password`): `demo@example.com` (owner of "Studio Pop" and "Side Project"), `member@example.com` (a plain member of "Studio Pop"), `client@example.com` (a client of "Studio Pop" who accepted an invitation).
- Elena's card has two projects: "Elena Design website" (Active, with four stages: two Done, one In progress, one To do) and "Brand refresh" (Planning, so hidden from her portal).
- The website has two deliverables: "Homepage mockups" (Shared, on the Design stage) and "Staging site" (Draft), and two updates: "Design approved" (Shared) and "Staging link coming soon" (Draft).
- "Homepage mockups" has an old approval round where Elena asked for changes ("The logo is too small on mobile.") and a new one still waiting for her, so her portal shows "1 waiting for you".
- The website also has two files, "Project brief" (Shared) and "Brand colours" (Draft); seeding writes their bytes to the file storage, so RustFS must be running (`docker compose up -d`) for `--seed` and for F5.
- The website also has a conversation (7 messages over two days between Ana, Mihai and Elena, one edited, one deleted): Elena and Ana each have 1 unread message, Mihai (the member) 5.
- "Pat Pending" in "Studio Pop" has an invitation waiting, openable at `/invite/dev-pending-invitation-token` without any email.
- Elena's card also has three meetings with Ana as organiser, all already "sent" to Google (there is no Google behind the demo data): "Design review" tomorrow at 10:00 UTC with a Meet link, "Branding chat" in three days, called off, and "Kick-off call" two days ago.
