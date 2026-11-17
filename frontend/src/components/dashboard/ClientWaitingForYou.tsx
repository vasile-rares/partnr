import { ChevronRightIcon } from "lucide-react";
import Link from "next/link";
import { DividedList } from "@/components/layout/DividedList";
import { Section } from "@/components/layout/Section";

export type WaitingDeliverable = {
  id: string;
  title: string;
  note: string | null;
  projectId: string;
  projectName: string;
};

/** Shown only when something needs an answer; the rest of the home page stays quiet otherwise. */
export function ClientWaitingForYou({
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
    <Section title="Waiting for you" className="client-waiting">
      <DividedList className="client-waiting-list">
        {items.map((item) => (
          <li key={item.id} className="client-waiting-item">
            <Link
              href={`/w/${workspaceId}/projects/${item.projectId}?tab=deliverables`}
              className="client-waiting-link flex items-center justify-between gap-4 p-4 hover:bg-muted/50"
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
      </DividedList>
    </Section>
  );
}
