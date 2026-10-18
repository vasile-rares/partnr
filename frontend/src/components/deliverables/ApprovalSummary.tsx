import { ApprovalStatusBadge } from "@/components/deliverables/ApprovalStatusBadge";
import type { ApprovalRound, Deliverable } from "@/lib/deliverables/types";
import { formatDate } from "@/lib/format";

const day = (iso: string): string => formatDate(iso.slice(0, 10));

/** Where the approval stands, in the client's words, and every round that led here. Read-only. */
export function ApprovalSummary({ deliverable }: { deliverable: Deliverable }) {
  const { approvals, approvalStatus } = deliverable;
  if (approvals.length === 0) {
    return null;
  }
  // The round that decides what is shown: the latest one that was not withdrawn.
  const current = approvals.find((round) => round.status !== "Withdrawn");

  return (
    <div className="mt-3 flex flex-col gap-2 text-sm">
      {current ? (
        <div className="flex flex-col gap-1">
          <p className="flex flex-wrap items-center gap-2">
            <ApprovalStatusBadge status={current.status} />
            {current.resolvedAtUtc ? <span className="text-muted-foreground">{day(current.resolvedAtUtc)}</span> : null}
          </p>
          {approvalStatus === "Pending" ? (
            <>
              {current.note ? <p className="text-muted-foreground">You asked: “{current.note}”</p> : null}
              <p className="text-muted-foreground">Withdraw the request to edit this deliverable. Unsharing it withdraws the request.</p>
            </>
          ) : current.message ? (
            <p className="whitespace-pre-line text-muted-foreground">“{current.message}”</p>
          ) : null}
        </div>
      ) : null}
      <details>
        <summary className="w-fit cursor-pointer rounded-sm text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
          Approval history ({approvals.length})
        </summary>
        <ol className="mt-2 flex flex-col divide-y rounded-lg border">
          {approvals.map((round) => (
            <HistoryRow key={round.id} round={round} />
          ))}
        </ol>
      </details>
    </div>
  );
}

function HistoryRow({ round }: { round: ApprovalRound }) {
  return (
    <li className="flex flex-col gap-1 p-3">
      <p className="flex flex-wrap items-center gap-2">
        <ApprovalStatusBadge status={round.status} />
        <span className="text-muted-foreground">
          Asked {day(round.requestedAtUtc)}
          {round.resolvedAtUtc ? ` · ${round.status === "Withdrawn" ? "withdrawn" : "answered"} ${day(round.resolvedAtUtc)}` : null}
        </span>
      </p>
      {round.note ? <p className="text-muted-foreground">You asked: “{round.note}”</p> : null}
      {round.message ? <p className="whitespace-pre-line text-muted-foreground">Client: “{round.message}”</p> : null}
    </li>
  );
}
