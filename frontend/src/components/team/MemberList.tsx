"use client";

import { UserMinusIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { toast } from "sonner";
import { IconButton } from "@/components/actions/IconButton";
import { FormError } from "@/components/forms/FormMessage";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/reui/badge";
import { sendJson } from "@/lib/api/client";
import { formatDate, initials } from "@/lib/format";
import type { Member } from "@/lib/team/types";

type MemberListProps = {
  workspaceId: string;
  members: Member[];
  currentUserId: string;
  /** The owner removes members; everyone else only reads the list. */
  canManage: boolean;
};

export function MemberList({ workspaceId, members, currentUserId, canManage }: MemberListProps) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [removing, setRemoving] = useState<Member | null>(null);
  const [message, setMessage] = useState<string>();

  function remove(member: Member) {
    startTransition(async () => {
      setMessage(undefined);
      const result = await sendJson("DELETE", `/api/workspaces/${workspaceId}/members/${member.userId}`);
      setRemoving(null);
      if (result.ok) {
        toast.success(`${member.name} removed from the team`);
        router.refresh();
      } else {
        // 409 says why (still organises meetings), so it is shown, not swallowed.
        setMessage(result.message);
      }
    });
  }

  return (
    <>
      <FormError message={message} />
      <ul className="member-list flex flex-col divide-y rounded-lg border">
        {members.map((member) => (
          <li key={member.userId} className="member-item flex items-center gap-3 p-4">
            <Avatar className="size-9">
              <AvatarFallback>{initials(member.name)}</AvatarFallback>
            </Avatar>
            <div className="member-info min-w-0 flex-1">
              <p className="font-medium">
                {member.name}
                {member.userId === currentUserId ? <span className="font-normal text-muted-foreground"> (you)</span> : null}
              </p>
              <p className="break-all text-sm text-muted-foreground">{member.email}</p>
            </div>
            <p className="member-joined hidden text-sm text-muted-foreground sm:block">Joined {formatDate(member.joinedAtUtc.slice(0, 10))}</p>
            <Badge variant={member.role === "Owner" ? "secondary" : "outline"}>{member.role}</Badge>
            {canManage ? (
              member.role === "Owner" ? (
                // The owner stays, but the same room is kept so the dates and roles line up down the list.
                <span aria-hidden className="size-8 shrink-0" />
              ) : (
                <IconButton
                  icon={UserMinusIcon}
                  tooltip="Remove"
                  aria-label={`Remove ${member.name}`}
                  disabled={pending}
                  onClick={() => setRemoving(member)}
                />
              )
            ) : null}
          </li>
        ))}
      </ul>

      <AlertDialog open={removing !== null} onOpenChange={(next) => (next ? null : setRemoving(null))}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove {removing?.name} from the team?</AlertDialogTitle>
            <AlertDialogDescription>
              They lose access to this workspace at once. What they wrote stays under their name, and you can invite them again later.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep them</AlertDialogCancel>
            <AlertDialogAction onClick={() => removing && remove(removing)}>Remove from the team</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
