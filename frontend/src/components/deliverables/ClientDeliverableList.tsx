import { ExternalLinkIcon } from "lucide-react";
import { ClientApprovalPanel } from "@/components/deliverables/ClientApprovalPanel";
import { DividedList } from "@/components/layout/DividedList";
import { Section } from "@/components/layout/Section";
import type { ClientDeliverable } from "@/lib/deliverables/types";

type Props = {
  workspaceId: string;
  workspaceName: string;
  projectId: string;
  deliverables: ClientDeliverable[];
};

/** What the members shared for this project, each with the client's side of its approval. */
export function ClientDeliverableList({ workspaceId, workspaceName, projectId, deliverables }: Props) {
  return (
    <Section title="Deliverables" className="client-deliverables">
      <DividedList className="client-deliverables-list">
        {deliverables.map((deliverable) => (
          <li key={deliverable.id} className="client-deliverables-item p-4">
            <p className="font-medium">
              <a
                href={deliverable.url}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1.5 hover:underline"
              >
                {deliverable.title}
                <ExternalLinkIcon aria-hidden className="size-4 text-muted-foreground" />
                <span className="sr-only">(opens in a new tab)</span>
              </a>
            </p>
            {deliverable.description ? (
              <p className="mt-1 whitespace-pre-line text-sm text-muted-foreground">{deliverable.description}</p>
            ) : null}
            {deliverable.stageName ? <p className="mt-2 text-sm text-muted-foreground">Stage: {deliverable.stageName}</p> : null}
            <ClientApprovalPanel
              workspaceId={workspaceId}
              projectId={projectId}
              deliverable={deliverable}
              workspaceName={workspaceName}
            />
          </li>
        ))}
      </DividedList>
    </Section>
  );
}
