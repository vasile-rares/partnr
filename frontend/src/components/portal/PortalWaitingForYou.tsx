import { ChevronRightIcon } from "lucide-react";
import Link from "next/link";

export type WaitingDeliverable = {
  id: string;
  title: string;
  note: string | null;
  projectId: string;
  projectName: string;
};

/** Shown only when something needs an answer; the rest of the home page stays quiet otherwise. */
export function PortalWaitingForYou({
  workspaceId,
  workspaceName,
  items,
}: {
  workspaceId: string;
  workspaceName: string;
  items: WaitingDeliverable[];
}) {
  if (items.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="waiting-title">
      <h2 id="waiting-title" className="mb-4 font-heading text-xl font-semibold">
        Waiting for you
      </h2>
      <ul className="flex flex-col divide-y rounded-lg border">
        {items.map((item) => (
          <li key={item.id}>
            <Link
              href={`/portal/${workspaceId}/projects/${item.projectId}?tab=deliverables`}
              className="flex items-center justify-between gap-4 p-4 hover:bg-muted/50"
            >
              <div className="min-w-0">
                <p className="font-medium">{item.title}</p>
                <p className="text-sm text-muted-foreground">{item.projectName}</p>
                {item.note ? (
                  <p className="mt-1 text-sm text-muted-foreground">
                    {workspaceName} asks: “{item.note}”
                  </p>
                ) : null}
              </div>
              <span className="flex shrink-0 items-center gap-1 text-sm font-medium">
                Review
                <ChevronRightIcon aria-hidden className="size-4" />
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
