import Link from "next/link";
import { DividedList } from "@/components/layout/DividedList";
import { Section } from "@/components/layout/Section";
import { formatDate } from "@/lib/format";

export type LatestUpdate = {
  id: string;
  title: string;
  body: string | null;
  sharedAtUtc: string;
  projectId: string;
  projectName: string;
};

/** Hidden until the members have shared something. */
export function ClientLatestUpdates({ workspaceId, updates }: { workspaceId: string; updates: LatestUpdate[] }) {
  if (updates.length === 0) {
    return null;
  }

  return (
    <Section title="Latest updates" className="client-latest-updates">
      <DividedList ordered className="client-latest-updates-list">
        {updates.map((update) => (
          <li key={update.id} className="client-latest-updates-item p-4">
            <p className="font-medium">{update.title}</p>
            {update.body ? <p className="mt-1 line-clamp-2 whitespace-pre-line text-sm text-muted-foreground">{update.body}</p> : null}
            <p className="mt-2 text-sm text-muted-foreground">
              <Link href={`/w/${workspaceId}/projects/${update.projectId}?tab=updates`} className="hover:underline">
                {update.projectName}
              </Link>
              {" · "}
              <time dateTime={update.sharedAtUtc}>{formatDate(update.sharedAtUtc.slice(0, 10))}</time>
            </p>
          </li>
        ))}
      </DividedList>
    </Section>
  );
}
