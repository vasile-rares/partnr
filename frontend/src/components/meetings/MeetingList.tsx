"use client";

import { PencilIcon, RefreshCwIcon, VideoIcon, XIcon } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, useTransition } from "react";
import { IconButton } from "@/components/actions/IconButton";
import { FormError } from "@/components/forms/FormMessage";
import { MeetingForm, type MeetingProjectOption } from "@/components/meetings/MeetingForm";
import { MeetingTime } from "@/components/meetings/MeetingTime";
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
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { sendJson } from "@/lib/api/client";
import { durationLabel, durationOf } from "@/lib/meetings/time";
import type { Meeting } from "@/lib/meetings/types";

const SYNC_REFRESH_MS = 4000;

type MeetingListProps = {
  workspaceId: string;
  meetings: Meeting[];
  clients: { id: string; displayName: string }[];
  projects: MeetingProjectOption[];
  /** What the server thinks "now" is, so the first paint in the browser matches what it sent. */
  now: string;
  /** Said when the list is empty. */
  empty: string;
  /** The client's own page already says whose meetings these are. */
  showClient?: boolean;
};

/** Each change is one request, then the page refreshes. A meeting that is over or called off is history and has no actions. */
export function MeetingList({ workspaceId, meetings, clients, projects, now, empty, showClient = true }: MeetingListProps) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [editing, setEditing] = useState<Meeting | null>(null);
  const [cancelling, setCancelling] = useState<Meeting | null>(null);
  const [message, setMessage] = useState<string>();
  const base = `/api/workspaces/${workspaceId}/meetings`;

  function run(path: string, onDone?: () => void) {
    startTransition(async () => {
      setMessage(undefined);
      const result = await sendJson("POST", `${base}/${path}`);
      if (result.ok) {
        onDone?.();
        router.refresh();
      } else {
        setMessage(result.message);
      }
    });
  }

  const waitingForGoogle = meetings.some((meeting) => meeting.syncStatus === "Pending" && Date.parse(meeting.endsAtUtc) > Date.parse(now));

  // The background sync answers within seconds, so the page asks again until nothing is waiting: the Meet link appears on its own.
  useEffect(() => {
    if (!waitingForGoogle) {
      return;
    }
    const timer = setInterval(() => router.refresh(), SYNC_REFRESH_MS);
    return () => clearInterval(timer);
  }, [waitingForGoogle, router]);

  if (meetings.length === 0) {
    return <p className="meeting-list-empty rounded-lg border border-dashed p-6 text-sm text-muted-foreground">{empty}</p>;
  }

  const nowMs = Date.parse(now);

  return (
    <>
      <FormError message={message} />
      <ul className="meeting-list flex flex-col divide-y rounded-lg border">
        {meetings.map((meeting) => {
          const cancelled = meeting.status === "Cancelled";
          const over = Date.parse(meeting.endsAtUtc) <= nowMs;
          const open = !cancelled && !over;
          // A problem still matters while the meeting has not happened: the client may be waiting for an invitation, or for news that it was called off.
          const unsent = !over && meeting.syncStatus !== "Synced";

          return (
            <li key={meeting.id} className="meeting-item flex flex-wrap items-start justify-between gap-x-4 gap-y-3 p-4">
              <div className="meeting-info min-w-0">
                <div className="meeting-title-row flex flex-wrap items-center gap-2">
                  <p className="font-medium">{meeting.title}</p>
                  {cancelled ? <Badge variant="outline">Cancelled</Badge> : null}
                  {unsent ? <SyncBadge meeting={meeting} /> : null}
                </div>
                <MeetingTime startsAtUtc={meeting.startsAtUtc} endsAtUtc={meeting.endsAtUtc} className="mt-1 block text-sm" />
                <p className="meeting-meta mt-1 text-sm text-muted-foreground">
                  {showClient ? (
                    <Link href={`/w/${workspaceId}/clients/${meeting.client.id}`} className="hover:text-foreground hover:underline">
                      {meeting.client.displayName}
                    </Link>
                  ) : null}
                  {showClient ? " · " : null}
                  {meeting.project ? (
                    <>
                      <Link href={`/w/${workspaceId}/projects/${meeting.project.id}`} className="hover:text-foreground hover:underline">
                        {meeting.project.name}
                      </Link>
                      {" · "}
                    </>
                  ) : null}
                  {durationLabel(durationOf(meeting))}
                </p>
                {meeting.description ? <p className="meeting-notes mt-2 whitespace-pre-line text-sm text-muted-foreground">{meeting.description}</p> : null}
              </div>

              <div className="meeting-actions flex items-center gap-1">
                {open && meeting.meetUrl ? (
                  <a href={meeting.meetUrl} target="_blank" rel="noopener noreferrer" className={buttonVariants({ size: "sm" })}>
                    <VideoIcon data-icon="inline-start" />
                    Join
                    <span className="sr-only"> {meeting.title} on Google Meet</span>
                  </a>
                ) : null}
                {!over && meeting.syncStatus === "Failed" ? (
                  <Button type="button" variant="outline" size="sm" disabled={pending} onClick={() => run(`${meeting.id}/retry-sync`)}>
                    <RefreshCwIcon data-icon="inline-start" />
                    Retry sync
                    <span className="sr-only"> for {meeting.title}</span>
                  </Button>
                ) : null}
                {open ? (
                  <>
                    <IconButton icon={PencilIcon} tooltip="Edit" aria-label={`Edit ${meeting.title}`} disabled={pending} onClick={() => setEditing(meeting)} />
                    <IconButton icon={XIcon} tooltip="Cancel" aria-label={`Cancel ${meeting.title}`} disabled={pending} onClick={() => setCancelling(meeting)} />
                  </>
                ) : null}
              </div>
            </li>
          );
        })}
      </ul>

      <Dialog open={editing !== null} onOpenChange={(next) => (next ? null : setEditing(null))}>
        <DialogContent className="edit-meeting-dialog max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Edit meeting</DialogTitle>
            <DialogDescription>Google tells your client about any change to the time.</DialogDescription>
          </DialogHeader>
          {editing ? (
            <MeetingForm workspaceId={workspaceId} clients={clients} projects={projects} meeting={editing} onDone={() => setEditing(null)} />
          ) : null}
        </DialogContent>
      </Dialog>

      <AlertDialog open={cancelling !== null} onOpenChange={(next) => (next ? null : setCancelling(null))}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Cancel this meeting?</AlertDialogTitle>
            <AlertDialogDescription>
              Google tells {cancelling?.client.displayName} that “{cancelling?.title}” is cancelled. It stays in your history, under Past.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep the meeting</AlertDialogCancel>
            <AlertDialogAction onClick={() => cancelling && run(`${cancelling.id}/cancel`, () => setCancelling(null))}>
              Cancel meeting
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

/** Where the event stands in Google, for a meeting that is not in step with it. */
function SyncBadge({ meeting }: { meeting: Meeting }) {
  if (meeting.syncStatus === "Failed") {
    return <Badge variant="destructive">Couldn&apos;t reach Google</Badge>;
  }
  if (meeting.syncStatus === "NeedsReconnect") {
    return (
      <Link href="/account" className="meeting-reconnect">
        <Badge variant="destructive">Reconnect Google</Badge>
      </Link>
    );
  }

  return <Badge variant="secondary">{meeting.status === "Cancelled" ? "Cancelling in Google…" : "Sending to Google…"}</Badge>;
}
