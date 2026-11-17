# Client invitations

How an invited client gets into the portal. What they see there is in [portal.md](portal.md). Decisions: `decisions.md`, "Invitații pentru clienți".

## API

- **Sending** (`ClientInvitationService`, members): `POST /api/workspaces/{id}/clients/{clientId}/invitation` sends one, and sending again cancels the open one first (a unique index allows one open invitation per client) and makes a new link; `DELETE .../invitation` cancels the open one. An archived client cannot be invited and a client who already has an account has nothing to invite (409). Archiving a client cancels its open invitation, so a link still in someone's inbox stops working.
- The row (`ClientInvitation`, table `client_invitations`) is saved **before** the email goes out (through `IEmailSender`, so Mailpit in development): if sending fails the member can resend, but a delivered link always has an invitation behind it.
- **The link** carries a `SecureToken`; only its SHA-256 hash is stored, so a leaked database leaks no working links. It is valid for **7 days** (`ClientInvitation.Validity`) and is bound to the address it was sent to. Its status (`Pending`, `Accepted`, `Cancelled`, `Expired`) is worked out from the dates.
- **Reading and using it** (`InvitationService`, `/api/invitations/{token}`, the token is the credential):
  - `GET` is public: the workspace's name, the address, the status, whether an account with that address exists, and when it expires. An unknown or malformed token is a 404.
  - `POST accept` needs a signed-in account **with the invited address** (403 for another address).
  - `POST register` is public: it creates the account with the invited address (409 when one already exists: sign in instead), saves the profile (name, optional phone and company), accepts and signs in.
  - A used, cancelled or expired link answers 409 with the reason. A member of the workspace cannot also be its client, and an account already linked to a card of that workspace cannot be linked again (both 409).
- **Accepting** links the client card to the account (`Client.LinkTo`), marks the invitation accepted, writes the "invitation accepted" notification for the member who sent it ([notifications.md](notifications.md)) and confirms the account's email (using the emailed link proves the address).
- **A card's details are locked** while an invitation is pending (so it keeps matching the address) and once the client has an account (the name, email, phone and company are then the account's own, and the card shows them). The update request may still send them back unchanged; only a real difference answers 409, and the notes always go through. `ClientResponse` carries `detailsLocked` and the portal status (`NotInvited`, `Invited` with its expiry, `Expired`, `Active`), built by `ClientResponseBuilder`.

## Web app

- **Freelancer**: `AddClientForm` (in `AddClientDialog`) adds the client and invites them in one go; if the invitation fails, the card exists anyway and the browser lands on the client's page with `?invite=failed`, where "Send invitation" says what happened. On the client's page `ClientPortalCard` shows the portal status and `ClientPortalActions`: "Send invitation" / "Resend invitation" / "Send a new invitation" and Cancel while one is pending. `PortalStatusBadge` shows the same status in the clients list.
- **Invited person**: `/invite/[token]` (`app/(auth)/invite/[token]/page.tsx`, public, listed in the `proxy.ts` matcher exclusions) answers every case on the server: not found, already used (with a link to the portal), cancelled, expired; otherwise "Join {workspace}" with, depending on who is looking:
  - signed in with the invited address: `AcceptInvitationButton`;
  - signed in with another address: an alert and `SwitchAccountButton`;
  - not signed in, an account exists: "Sign in to accept" (`/login?next=/invite/{token}`);
  - not signed in, no account: `InvitationRegisterForm` (the address is fixed).
- Data in `src/lib/invitations/server.ts` (`getInvitation()`).
- In e2e tests the invitation email is read back from Mailpit (`e2e/mailpit.ts`); the seed has a pending one at `/invite/dev-pending-invitation-token` ([seed-data.md](seed-data.md)).
