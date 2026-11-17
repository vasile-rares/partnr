import { DividedList } from "@/components/layout/DividedList";
import { Section } from "@/components/layout/Section";
import { StageStatusBadge } from "@/components/projects/StageStatusBadge";
import { formatDate } from "@/lib/format";
import { orderByDate } from "@/lib/projects/timeline";
import type { Stage } from "@/lib/projects/types";

/** The stages to read, in date order. The members edit them on the timeline of their own page. */
export function ClientStageList({ stages }: { stages: Stage[] }) {
  if (stages.length === 0) {
    return null;
  }

  return (
    <Section title="Stages" className="client-stages">
      <DividedList ordered className="client-stages-list">
        {orderByDate(stages).map((stage) => (
          <li key={stage.id} className="client-stages-item p-4">
            <div className="client-stages-header flex flex-wrap items-center justify-between gap-3">
              <p className="font-medium">{stage.name}</p>
              <StageStatusBadge status={stage.status} />
            </div>
            {stage.description ? <p className="mt-1 text-sm text-muted-foreground">{stage.description}</p> : null}
            {stage.dueDate ? <p className="mt-2 text-sm text-muted-foreground">Due {formatDate(stage.dueDate)}</p> : null}
          </li>
        ))}
      </DividedList>
    </Section>
  );
}
