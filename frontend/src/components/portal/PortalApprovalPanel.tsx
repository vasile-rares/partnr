"use client";

import { useRouter } from "next/navigation";
import { useRef, useState, useTransition } from "react";
import { ApprovalStatusBadge } from "@/components/deliverables/ApprovalStatusBadge";
import { FormError } from "@/components/forms/FormMessage";
import { TextAreaField } from "@/components/forms/TextAreaField";
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
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import { formatDate } from "@/lib/format";
import type { PortalApproval, PortalDeliverable } from "@/lib/portal/types";

type Props = {
  projectId: string;
  deliverable: PortalDeliverable;
  /** Who is asking, in the client's eyes. */
  workspaceName: string;
};

type Answer = PortalApproval["history"][number];

const day = (iso: string): string => formatDate(iso.slice(0, 10));

/** What the client was asked about one deliverable, the buttons to answer it, and what they said before. */
export function PortalApprovalPanel({ projectId, deliverable, workspaceName }: Props) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState<string>();
  const form = useRef<HTMLFormElement>(null);
  const { approval } = deliverable;

  if (approval.status === "None") {
    return null;
  }

  const open = approval.pending;
  const latest = approval.history[0];
  // Once answered, the latest answer is on top; before that, every earlier answer is history.
  const earlier = open ? approval.history : approval.history.slice(1);

  function answer(action: "approve" | "request-changes") {
    if (!open) {
      return;
    }
    const text = String(new FormData(form.current ?? undefined).get("message") ?? "").trim();
    if (action === "request-changes" && text === "") {
      setMessage("Say what should change.");
      return;
    }

    startTransition(async () => {
      setMessage(undefined);
      const result = await sendJson("POST", `/api/portal/projects/${projectId}/deliverables/${deliverable.id}/approvals/${open.id}/${action}`, {
        message: text,
      });
      if (result.ok) {
        router.refresh();
      } else {
        setMessage(result.message);
      }
    });
  }

  return (
    <div className="mt-3 flex flex-col gap-3 text-sm">
      {open ? (
        <form ref={form} onSubmit={(event) => event.preventDefault()} className="flex flex-col gap-3 rounded-lg border p-3">
          <p className="flex flex-wrap items-center gap-2">
            <ApprovalStatusBadge status="Pending" />
            <span className="text-muted-foreground">Asked {day(open.requestedAtUtc)}</span>
          </p>
          <p>
            {open.note ? `${workspaceName} asks: “${open.note}”` : `${workspaceName} would like your approval.`}
          </p>
          <TextAreaField
            label="Your message"
            name="message"
            maxLength={2000}
            rows={3}
            hint="Optional when you approve. Say what should change if you ask for changes."
          />
          <FormError message={message} />
          <div className="flex flex-wrap gap-2">
            <ApproveButton title={deliverable.title} disabled={pending} onConfirm={() => answer("approve")} />
            <Button
              type="button"
              variant="outline"
              disabled={pending}
              aria-label={`Request changes to ${deliverable.title}`}
              onClick={() => answer("request-changes")}
            >
              Request changes
            </Button>
          </div>
        </form>
      ) : latest ? (
        <div className="flex flex-col gap-1">
          <p className="flex flex-wrap items-center gap-2">
            <ApprovalStatusBadge status={latest.decision} />
            <span className="text-muted-foreground">Answered {day(latest.decidedAtUtc)}</span>
          </p>
          {latest.message ? <p className="whitespace-pre-line text-muted-foreground">You said: “{latest.message}”</p> : null}
        </div>
      ) : null}

      {earlier.length > 0 ? (
        <details>
          <summary className="w-fit cursor-pointer rounded-sm text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
            Earlier answers ({earlier.length})
          </summary>
          <ol className="mt-2 flex flex-col divide-y rounded-lg border">
            {earlier.map((round) => (
              <EarlierAnswer key={round.requestedAtUtc} round={round} workspaceName={workspaceName} />
            ))}
          </ol>
        </details>
      ) : null}
    </div>
  );
}

function EarlierAnswer({ round, workspaceName }: { round: Answer; workspaceName: string }) {
  return (
    <li className="flex flex-col gap-1 p-3">
      <p className="flex flex-wrap items-center gap-2">
        <ApprovalStatusBadge status={round.decision} />
        <span className="text-muted-foreground">Answered {day(round.decidedAtUtc)}</span>
      </p>
      {round.note ? <p className="text-muted-foreground">{workspaceName} asked: “{round.note}”</p> : null}
      {round.message ? <p className="whitespace-pre-line text-muted-foreground">You said: “{round.message}”</p> : null}
    </li>
  );
}

// An approval is final for its request, so one stray click must not send it.
function ApproveButton({ title, disabled, onConfirm }: { title: string; disabled: boolean; onConfirm: () => void }) {
  return (
    <AlertDialog>
      <AlertDialogTrigger render={<Button type="button" disabled={disabled} aria-label={`Approve ${title}`} />}>Approve</AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Approve this deliverable?</AlertDialogTitle>
          <AlertDialogDescription>
            “{title}” will be marked as approved. Your answer is final for this request.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Not yet</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>Yes, approve</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
