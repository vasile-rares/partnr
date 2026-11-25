"use client";

import { MailIcon, RotateCwIcon, XIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { toast } from "sonner";
import { IconButton } from "@/components/actions/IconButton";
import { FormError } from "@/components/forms/FormMessage";
import { Badge } from "@/components/ui/badge";
import { sendJson } from "@/lib/api/client";
import { formatDate } from "@/lib/format";
import type { MemberInvitation } from "@/lib/team/types";

/** The open invitations, for the owner. Resending is inviting the same address again, which replaces the open one. */
export function InvitationList({ workspaceId, invitations }: { workspaceId: string; invitations: MemberInvitation[] }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState<string>();
  const base = `/api/workspaces/${workspaceId}/member-invitations`;

  function run(request: () => ReturnType<typeof sendJson>, done: string) {
    startTransition(async () => {
      setMessage(undefined);
      const result = await request();
      if (result.ok) {
        toast.success(done);
        router.refresh();
      } else {
        setMessage(result.message);
      }
    });
  }

  if (invitations.length === 0) {
    return (
      <p className="invitation-list-empty rounded-lg border border-dashed p-6 text-sm text-muted-foreground">
        No pending invitations. Invite someone to give them access to all the clients and projects of this workspace.
      </p>
    );
  }

  return (
    <>
      <FormError message={message} />
      <ul className="invitation-list flex flex-col divide-y rounded-lg border">
        {invitations.map((invitation) => (
          <li key={invitation.id} className="invitation-item flex items-center gap-3 p-4">
            <span className="invitation-icon flex size-9 shrink-0 items-center justify-center rounded-full bg-muted">
              <MailIcon className="size-4 text-muted-foreground" aria-hidden />
            </span>
            <div className="invitation-info min-w-0 flex-1">
              <p className="break-all font-medium">{invitation.email}</p>
              <p className="text-sm text-muted-foreground">
                Sent {formatDate(invitation.createdAtUtc.slice(0, 10))} ·{" "}
                {invitation.status === "Expired" ? "expired" : "expires"} {formatDate(invitation.expiresAtUtc.slice(0, 10))}
              </p>
            </div>
            {invitation.status === "Expired" ? <Badge variant="outline">Expired</Badge> : null}
            <IconButton
              icon={RotateCwIcon}
              tooltip="Resend"
              aria-label={`Resend invitation to ${invitation.email}`}
              disabled={pending}
              onClick={() => run(() => sendJson("POST", base, { email: invitation.email }), `Invitation sent to ${invitation.email}`)}
            />
            <IconButton
              icon={XIcon}
              tooltip="Cancel"
              aria-label={`Cancel invitation to ${invitation.email}`}
              disabled={pending}
              onClick={() => run(() => sendJson("DELETE", `${base}/${invitation.id}`), "Invitation cancelled")}
            />
          </li>
        ))}
      </ul>
    </>
  );
}
