import { CircleAlertIcon, VideoIcon } from "lucide-react";
import Link from "next/link";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { buttonVariants } from "@/components/ui/button";
import type { GoogleConnection } from "@/lib/google/types";

/** Above the meetings while Google is not in the way: nothing when it is connected and well. */
export function GoogleCalendarNotice({ google }: { google: GoogleConnection }) {
  if (google.available && google.status === "Connected") {
    return null;
  }

  if (!google.available) {
    return (
      <Alert className="google-notice mb-8">
        <VideoIcon />
        <AlertTitle>Google Calendar is not set up</AlertTitle>
        <AlertDescription>Meetings are created in Google Calendar, which this server is not connected to, so none can be scheduled.</AlertDescription>
      </Alert>
    );
  }

  const lapsed = google.status === "NeedsReconnect";

  return (
    <Alert variant={lapsed ? "destructive" : "default"} className="google-notice mb-8">
      {lapsed ? <CircleAlertIcon /> : <VideoIcon />}
      <AlertTitle>{lapsed ? "Connect Google again" : "Connect your Google Calendar to schedule meetings"}</AlertTitle>
      <AlertDescription>
        <p>
          {lapsed
            ? "Google no longer lets Partnr use your calendar. Changes to your meetings wait until you connect again."
            : "Meetings are created in your Google Calendar with a Meet link, and Google invites your clients and sends the reminders."}
        </p>
        <Link href="/account" className={buttonVariants({ variant: lapsed ? "outline" : "default", size: "sm" })}>
          {lapsed ? "Reconnect Google Calendar" : "Connect Google Calendar"}
        </Link>
      </AlertDescription>
    </Alert>
  );
}
