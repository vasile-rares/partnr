import { CheckCircle2Icon, CircleAlertIcon } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/reui/alert";
import type { GoogleResult } from "@/lib/google/types";

const messages: Record<GoogleResult, { title: string; description: string; failed: boolean }> = {
  connected: {
    title: "Google Calendar connected",
    description: "Meetings you schedule from now on are added to your calendar, and your client is invited by Google.",
    failed: false,
  },
  denied: {
    title: "Google Calendar was not connected",
    description: "You chose not to give access. You can connect it whenever you are ready.",
    failed: true,
  },
  permission: {
    title: "Calendar access is missing",
    description: "Partnr needs permission to create events in your calendar. Connect again and leave the calendar option ticked.",
    failed: true,
  },
  error: {
    title: "Google Calendar could not be connected",
    description: "Something went wrong on the way back from Google. Try connecting again.",
    failed: true,
  },
};

/** What happened on the way back from Google. The failed outcomes are alerts; a success is a quiet status. */
export function GoogleResultMessage({ result }: { result: GoogleResult }) {
  const { title, description, failed } = messages[result];

  return failed ? (
    <Alert variant="destructive" className="google-result mb-6 max-w-lg">
      <CircleAlertIcon />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>{description}</AlertDescription>
    </Alert>
  ) : (
    <div role="status" className="google-result mb-6 flex max-w-lg gap-2 rounded-lg border px-2.5 py-2 text-sm">
      <CheckCircle2Icon className="mt-0.5 size-4 shrink-0 text-status-done" aria-hidden />
      <div>
        <p className="font-medium">{title}</p>
        <p className="text-muted-foreground">{description}</p>
      </div>
    </div>
  );
}
