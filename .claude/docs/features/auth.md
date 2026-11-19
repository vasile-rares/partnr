# Auth, password reset and profile

The cross-cutting auth rules (cookie, fallback policy, `requireUser()` on every protected page) are in the root `CLAUDE.md` and `backend/CLAUDE.md`. This file describes the flows.

## API

- **Password reset** (`PasswordResetService`, `PasswordResetToken`, table `password_reset_tokens`): `POST /api/auth/forgot-password` always answers 204 and emails only an existing account: the service publishes `PasswordResetRequested` (address, name and the plain token) before its `SaveChanges` and the Notifications service writes and sends the email, so the response time does not show whether one went out; the link carries a `SecureToken` (only its hash is stored, as for invitations), works once, for an hour, and a newer link cancels it; one email a minute per account and a global rate limit (`RateLimitPolicies.PasswordReset`; per address is pointless while the web app's proxy hides the visitor's address). `GET /api/auth/reset-password/{token}` checks a link (404 not ours, 409 used / replaced / expired); `POST /api/auth/reset-password` sets the password through `IIdentityService.ResetPasswordAsync` (same password rules, lockout cleared, security stamp changed, so every other session is signed out) and signs this one in. Email confirmation is not built.
- The account profile (`PUT /api/auth/me`: full name, phone, company) is the user's own; the email is not part of it and cannot be changed yet. The phone is Identity's `PhoneNumber` column.

## Web app

- Pages and forms are in `src/app/(auth)/` and `src/components/auth/`; the session logic is in `src/lib/auth/`.
- `/login` (`LoginForm`, honours `?next=` so an invitation can send the person back) and `/register` (`RegisterForm`). The sign-in pages share `app/(auth)/layout.tsx`, which shows an invented sample project beside the form (`components/auth/ProjectTimeline.tsx`, static, not the real timeline) and a `ThemeToggle`.
- A forgotten password goes through `/forgot-password` (the same answer whether or not the address has an account) and the emailed `/reset-password/[token]` (checked on the server by `getPasswordResetLink` in `src/lib/passwordReset/server.ts`); both are public in `proxy.ts`.
- `getCurrentUser()` / `requireUser()` (`src/lib/auth/session.ts`) read `GET /api/auth/me`; sign-out is in `NavUser` at the foot of the sidebar.
- **Account page** (`/account`, `app/(shell)/account/page.tsx`): the email (read-only, "You can't change your email yet"), `ProfileForm` (full name, phone, company) and, only for someone who works in a workspace, the Google Calendar card ([google-calendar.md](google-calendar.md)). The profile is the account's own, so it lives outside `/w/` and works with no workspace at all; a client's name, phone and company on the freelancer's side come from it once they have an account ([invitations.md](invitations.md)).
