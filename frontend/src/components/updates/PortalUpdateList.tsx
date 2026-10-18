import { formatDate } from "@/lib/format";
import type { PortalProjectUpdate } from "@/lib/portal/types";

const VISIBLE = 5;

/** The most recent updates are open; the rest sit in a native <details>, so there is no script and no extra request. */
export function PortalUpdateList({ updates, workspaceName }: { updates: PortalProjectUpdate[]; workspaceName: string }) {
  const recent = updates.slice(0, VISIBLE);
  const earlier = updates.slice(VISIBLE);

  return (
    <section aria-labelledby="updates-title" className="mt-10">
      <h2 id="updates-title" className="mb-4 font-heading text-xl font-semibold">
        Updates
      </h2>

      {updates.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          No updates yet. {workspaceName} will post news here as the project moves.
        </p>
      ) : (
        <>
          <ol className="flex flex-col divide-y rounded-lg border">{recent.map(item)}</ol>
          {earlier.length > 0 ? (
            <details className="mt-3">
              <summary className="cursor-pointer rounded-sm text-sm font-medium text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                Show earlier updates ({earlier.length})
              </summary>
              <ol className="mt-3 flex flex-col divide-y rounded-lg border">{earlier.map(item)}</ol>
            </details>
          ) : null}
        </>
      )}
    </section>
  );
}

function item(update: PortalProjectUpdate) {
  return (
    <li key={update.id} className="p-4">
      <p className="font-medium">{update.title}</p>
      {update.body ? <p className="mt-1 whitespace-pre-line text-sm text-muted-foreground">{update.body}</p> : null}
      <p className="mt-2 text-sm text-muted-foreground">
        <time dateTime={update.sharedAtUtc}>{formatDate(update.sharedAtUtc.slice(0, 10))}</time>
      </p>
    </li>
  );
}
