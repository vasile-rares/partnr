import type { Metadata } from "next";
import Link from "next/link";
import type { ReactNode } from "react";
import { AcceptInvitationButton } from "@/components/invitations/AcceptInvitationButton";
import { InvitationRegisterForm } from "@/components/invitations/InvitationRegisterForm";
import { SwitchAccountButton } from "@/components/invitations/SwitchAccountButton";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { buttonVariants } from "@/components/ui/button";
import { getCurrentUser } from "@/lib/auth/session";
import { getInvitation } from "@/lib/invitations/server";
import { formatDateTimeUtc } from "@/lib/format";

export const metadata: Metadata = { title: "Invitation" };

// Public on purpose (see proxy.ts): the person opening the link may not have an account yet. The token in the URL is
// what proves they got the email; accepting still needs the invited address (checked by the API).
export default async function InvitePage({ params }: PageProps<"/invite/[token]">) {
  const { token } = await params;
  const invitation = await getInvitation(token);

  if (!invitation) {
    return (
      <Message title="Invitation not found">
        This link isn&apos;t valid. Check that you copied all of it from the email, or ask for a new invitation.
      </Message>
    );
  }

  const { workspaceName, email } = invitation;

  switch (invitation.status) {
    case "Accepted":
      return (
        <Message title="Invitation already used">
          This invitation from {workspaceName} was already accepted.
          <Link href="/portal" className={buttonVariants({ size: "lg", className: "mt-6 w-full" })}>
            Go to your portal
          </Link>
        </Message>
      );
    case "Cancelled":
      return (
        <Message title="Invitation cancelled">
          {workspaceName} cancelled this invitation. Ask them to send you a new one.
        </Message>
      );
    case "Expired":
      return (
        <Message title="Invitation expired">
          This invitation from {workspaceName} expired on {formatDateTimeUtc(invitation.expiresAtUtc)}. Ask them to send you a
          new one.
        </Message>
      );
  }

  const user = await getCurrentUser();
  const signedInAsInvited = user?.email.trim().toLowerCase() === email.trim().toLowerCase();

  return (
    <>
      <h1 className="mb-3 font-heading text-3xl font-semibold">Join {workspaceName}</h1>
      <p className="mb-8 text-muted-foreground">
        {workspaceName} invited <span className="font-medium text-foreground">{email}</span> to follow their projects in the
        portal.
      </p>

      {user && signedInAsInvited ? (
        <AcceptInvitationButton token={token} />
      ) : user ? (
        <div className="flex flex-col gap-6">
          <Alert>
            <AlertDescription>
              You are signed in as {user.email}, but this invitation is for {email}. Only that address can accept it.
            </AlertDescription>
          </Alert>
          <SwitchAccountButton />
        </div>
      ) : invitation.accountExists ? (
        <div className="flex flex-col gap-4">
          <p className="text-sm text-muted-foreground">You already have an account with this address. Sign in to accept.</p>
          <Link
            href={`/login?next=${encodeURIComponent(`/invite/${token}`)}`}
            className={buttonVariants({ size: "lg", className: "w-full" })}
          >
            Sign in to accept
          </Link>
        </div>
      ) : (
        <InvitationRegisterForm token={token} email={email} />
      )}
    </>
  );
}

function Message({ title, children }: { title: string; children: ReactNode }) {
  return (
    <>
      <h1 className="mb-3 font-heading text-3xl font-semibold">{title}</h1>
      <div className="flex flex-col text-muted-foreground">{children}</div>
    </>
  );
}
