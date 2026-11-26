"use client";

import { CircleAlertIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { ConnectGoogleButton } from "@/components/account/ConnectGoogleButton";
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
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Alert, AlertDescription, AlertTitle } from "@/components/reui/alert";
import { Badge } from "@/components/reui/badge";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import type { GoogleConnection } from "@/lib/google/types";

/** The freelancer's own Google Calendar: meetings are created there, and Google invites and reminds the client. */
export function GoogleCalendarCard({ connection }: { connection: GoogleConnection }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState<string>();
  const { available, status, email } = connection;

  function disconnect() {
    startTransition(async () => {
      setMessage(undefined);
      const result = await sendJson("DELETE", "/api/integrations/google");
      if (result.ok) {
        router.refresh();
      } else {
        setMessage(result.message);
      }
    });
  }

  return (
    <section aria-labelledby="google-calendar-title" className="google-calendar-card mt-12 flex max-w-lg flex-col gap-4 border-t pt-8">
      <div className="google-calendar-header flex items-center justify-between gap-3">
        <h2 id="google-calendar-title" className="font-heading text-xl font-semibold">
          Google Calendar
        </h2>
        {available ? (
          <Badge variant={status === "Connected" ? "success-light" : status === "NeedsReconnect" ? "destructive-light" : "secondary"}>
            {status === "Connected" ? "Connected" : status === "NeedsReconnect" ? "Needs attention" : "Not connected"}
          </Badge>
        ) : null}
      </div>

      <p className="text-sm text-muted-foreground">
        Meetings you schedule are added to your own Google Calendar with a Meet link. Google invites your client and sends the reminders.
      </p>

      {!available ? (
        <p className="text-sm text-muted-foreground">Google Calendar is not set up on this server, so meetings cannot be scheduled yet.</p>
      ) : null}

      {available && status === "Connected" ? (
        <p className="text-sm">
          Connected as <strong className="font-medium">{email}</strong>.
        </p>
      ) : null}

      {available && status === "NeedsReconnect" ? (
        <Alert variant="destructive">
          <CircleAlertIcon />
          <AlertTitle>Connect Google again</AlertTitle>
          <AlertDescription>
            Google no longer lets Partnr use your calendar{email ? ` (${email})` : ""}. Changes to your meetings are waiting, and are sent as
            soon as you connect again.
          </AlertDescription>
        </Alert>
      ) : null}

      <FormError message={message} />

      {available ? (
        <div className="google-calendar-actions flex flex-wrap gap-3">
          {status === "Connected" ? null : (
            <ConnectGoogleButton>{status === "NeedsReconnect" ? "Reconnect Google Calendar" : "Connect Google Calendar"}</ConnectGoogleButton>
          )}
          {status === "NotConnected" ? null : (
            <AlertDialog>
              <AlertDialogTrigger render={<Button type="button" variant="outline" disabled={pending} />}>Disconnect</AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Disconnect Google Calendar?</AlertDialogTitle>
                  <AlertDialogDescription>
                    Meetings already in your calendar stay there. Changes to meetings that have not been sent yet wait until you connect
                    again, and you cannot schedule new ones.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Keep it connected</AlertDialogCancel>
                  <AlertDialogAction onClick={disconnect}>Disconnect</AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          )}
        </div>
      ) : null}
    </section>
  );
}
