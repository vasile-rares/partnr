"use client";

import { PlusIcon, VideoIcon } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { ConnectGoogleButton } from "@/components/account/ConnectGoogleButton";
import { MeetingForm, type MeetingProjectOption } from "@/components/meetings/MeetingForm";
import { Button, buttonVariants } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import type { GoogleConnection } from "@/lib/google/types";

type NewMeetingDialogProps = {
  workspaceId: string;
  clients: { id: string; displayName: string }[];
  projects: MeetingProjectOption[];
  google: GoogleConnection;
  /** Preselected when the dialog opens from a client's page. */
  clientId?: string;
  variant?: "default" | "outline";
};

/** "New meeting". Without a connected Google Calendar there is nothing to schedule into, so the button connects it instead. */
export function NewMeetingDialog({ workspaceId, clients, projects, google, clientId, variant = "default" }: NewMeetingDialogProps) {
  const [open, setOpen] = useState(false);

  if (!google.available) {
    return (
      <Button variant={variant} disabled title="Google Calendar is not set up on this server.">
        <PlusIcon data-icon="inline-start" />
        New meeting
      </Button>
    );
  }

  if (google.status !== "Connected") {
    return (
      <ConnectGoogleButton variant={variant} returnTo={`/w/${workspaceId}/calendar`}>
        <VideoIcon data-icon="inline-start" />
        {google.status === "NeedsReconnect" ? "Reconnect Google to schedule" : "Connect Google to schedule"}
      </ConnectGoogleButton>
    );
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={<Button variant={variant} />}>
        <PlusIcon data-icon="inline-start" />
        New meeting
      </DialogTrigger>
      <DialogContent className="new-meeting-dialog max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>New meeting</DialogTitle>
          <DialogDescription>
            It is added to your Google Calendar with a Meet link. Google emails your client the invitation and the reminders.
          </DialogDescription>
        </DialogHeader>
        {clients.length === 0 ? (
          <div className="new-meeting-no-clients flex flex-col items-start gap-4">
            <p className="text-muted-foreground">A meeting is with a client. Add your first client to schedule one.</p>
            <Link href={`/w/${workspaceId}/clients`} className={buttonVariants()}>
              Go to clients
            </Link>
          </div>
        ) : (
          <MeetingForm workspaceId={workspaceId} clients={clients} projects={projects} clientId={clientId} onDone={() => setOpen(false)} />
        )}
      </DialogContent>
    </Dialog>
  );
}
