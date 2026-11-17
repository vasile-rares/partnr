import type { ReactNode } from "react";
import { DividedList } from "@/components/layout/DividedList";
import { Section } from "@/components/layout/Section";
import { formatDate } from "@/lib/format";
import type { ClientUpdate } from "@/lib/updates/types";

const VISIBLE = 5;

type Props = {
  updates: ClientUpdate[];
  workspaceName: string;
  /** Show only this many, with no disclosure for the rest: a glance, not the full list. */
  latest?: number;
  children?: ReactNode;
};

/** The most recent updates are open; the rest sit in a native <details>, so there is no script and no extra request. */
export function ClientUpdateList({ updates, workspaceName, latest, children }: Props) {
  const recent = updates.slice(0, latest ?? VISIBLE);
  const earlier = latest ? [] : updates.slice(VISIBLE);

  return (
    <Section title="Updates" className="client-updates">
      {updates.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          No updates yet. {workspaceName} will post news here as the project moves.
        </p>
      ) : (
        <>
          <DividedList ordered className="client-updates-list">
            {recent.map(item)}
          </DividedList>
          {earlier.length > 0 ? (
            <details className="client-updates-earlier mt-3">
              <summary className="cursor-pointer rounded-sm text-sm font-medium text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                Show earlier updates ({earlier.length})
              </summary>
              <DividedList ordered className="client-updates-earlier-list mt-3">
                {earlier.map(item)}
              </DividedList>
            </details>
          ) : null}
        </>
      )}
      {children}
    </Section>
  );
}

function item(update: ClientUpdate) {
  return (
    <li key={update.id} className="client-updates-item p-4">
      <p className="font-medium">{update.title}</p>
      {update.body ? <p className="mt-1 whitespace-pre-line text-sm text-muted-foreground">{update.body}</p> : null}
      <p className="mt-2 text-sm text-muted-foreground">
        <time dateTime={update.sharedAtUtc}>{formatDate(update.sharedAtUtc.slice(0, 10))}</time>
      </p>
    </li>
  );
}
