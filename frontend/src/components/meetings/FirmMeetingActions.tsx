"use client";

import { PencilIcon, RefreshCwIcon, XIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { IconButton } from "@/components/actions/IconButton";
import { FormError } from "@/components/forms/FormMessage";
import { JoinMeetingButton } from "@/components/meetings/JoinMeetingButton";
import { MeetingForm, type MeetingProjectOption } from "@/components/meetings/MeetingForm";
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
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { sendJson } from "@/lib/api/client";
import { toastDone } from "@/lib/toast";
import type { Meeting } from "@/lib/meetings/types";

type FirmMeetingActionsProps = {
  workspaceId: string;
  meeting: Meeting;
  clients: { id: string; displayName: string }[];
  projects: MeetingProjectOption[];
  /** What the server thinks "now" is, so the first paint in the browser matches what it sent. */
  now: string;
};

/**
 * What a member can do with a meeting: join, retry the sync, edit, cancel. Each change is one request, then the page refreshes.
 * A meeting that is over or called off is history and has no actions.
 */
export function FirmMeetingActions({ workspaceId, meeting, clients, projects, now }: FirmMeetingActionsProps) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [editing, setEditing] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [message, setMessage] = useState<string>();
  const base = `/api/workspaces/${workspaceId}/meetings`;

  const over = Date.parse(meeting.endsAtUtc) <= Date.parse(now);
  const open = meeting.status !== "Cancelled" && !over;
  const canJoin = open && meeting.meetUrl !== null;
  const canRetry = !over && meeting.syncStatus === "Failed";

  function run(path: string, onDone?: () => void) {
    startTransition(async () => {
      setMessage(undefined);
      const result = await sendJson("POST", `${base}/${path}`);
      if (result.ok) {
        onDone?.();
        toastDone("Meeting", "POST", path);
        router.refresh();
      } else {
        setMessage(result.message);
      }
    });
  }

  if (!canJoin && !canRetry && !open) {
    return null;
  }

  return (
    <div className="meeting-actions-block flex flex-col items-end gap-2">
      <div className="meeting-actions flex items-center gap-1">
        {canJoin ? <JoinMeetingButton title={meeting.title} url={meeting.meetUrl!} /> : null}
        {canRetry ? (
          <Button type="button" variant="outline" size="sm" disabled={pending} onClick={() => run(`${meeting.id}/retry-sync`)}>
            <RefreshCwIcon data-icon="inline-start" />
            Retry sync
            <span className="sr-only"> for {meeting.title}</span>
          </Button>
        ) : null}
        {open ? (
          <>
            <IconButton icon={PencilIcon} tooltip="Edit" aria-label={`Edit ${meeting.title}`} disabled={pending} onClick={() => setEditing(true)} />
            <IconButton icon={XIcon} tooltip="Cancel" aria-label={`Cancel ${meeting.title}`} disabled={pending} onClick={() => setCancelling(true)} />
          </>
        ) : null}
      </div>
      <FormError message={message} />

      <Dialog open={editing} onOpenChange={setEditing}>
        <DialogContent className="edit-meeting-dialog max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Edit meeting</DialogTitle>
            <DialogDescription>Google tells your client about any change to the time.</DialogDescription>
          </DialogHeader>
          <MeetingForm workspaceId={workspaceId} clients={clients} projects={projects} meeting={meeting} onDone={() => setEditing(false)} />
        </DialogContent>
      </Dialog>

      <AlertDialog open={cancelling} onOpenChange={setCancelling}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Cancel this meeting?</AlertDialogTitle>
            <AlertDialogDescription>
              Google tells {meeting.client.displayName} that “{meeting.title}” is cancelled. It stays on your calendar, marked as cancelled.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep the meeting</AlertDialogCancel>
            <AlertDialogAction onClick={() => run(`${meeting.id}/cancel`, () => setCancelling(false))}>Cancel meeting</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
