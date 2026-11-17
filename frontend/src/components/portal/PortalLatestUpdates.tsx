import Link from "next/link";
import { formatDate } from "@/lib/format";

export type LatestUpdate = {
  id: string;
  title: string;
  body: string | null;
  sharedAtUtc: string;
  projectId: string;
  projectName: string;
};

/** Hidden until the freelancer has shared something. */
export function PortalLatestUpdates({ workspaceId, updates }: { workspaceId: string; updates: LatestUpdate[] }) {
  if (updates.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="latest-updates-title" className="portal-latest-updates">
      <h2 id="latest-updates-title" className="mb-4 font-heading text-xl font-semibold">
        Latest updates
      </h2>
      <ol className="portal-latest-updates-list flex flex-col divide-y rounded-lg border">
        {updates.map((update) => (
          <li key={update.id} className="portal-latest-updates-item p-4">
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
      </ol>
    </section>
  );
}
