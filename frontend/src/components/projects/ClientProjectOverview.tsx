import { ClientStageList } from "@/components/projects/ClientStageList";
import { GoToTab } from "@/components/projects/ProjectTabs";
import { Alert, AlertDescription, AlertTitle } from "@/components/reui/alert";
import { Card, CardContent } from "@/components/ui/card";
import { ClientUpdateList } from "@/components/updates/ClientUpdateList";
import { standing } from "@/lib/projects/summary";
import type { ClientProject } from "@/lib/projects/types";

const LATEST_UPDATES = 3;

/** The client's first tab: what waits for them, where things stand, the latest news and the stages. */
export function ClientProjectOverview({ project }: { project: ClientProject }) {
  const { now, next } = standing(project.status, project.stages);

  return (
    <div className="client-project-overview flex flex-col gap-10">
      {project.description ? <p className="max-w-prose whitespace-pre-line">{project.description}</p> : null}

      {project.awaitingApproval > 0 ? (
        // role="status": worth announcing politely, but not an error, so not the assertive default of Alert.
        <Alert role="status" className="client-project-waiting">
          <AlertTitle>Waiting for you</AlertTitle>
          <AlertDescription>
            {project.awaitingApproval === 1
              ? "1 deliverable needs your approval."
              : `${project.awaitingApproval} deliverables need your approval.`}{" "}
            <GoToTab tab="deliverables">See what to approve</GoToTab>
          </AlertDescription>
        </Alert>
      ) : null}

      <Card className="client-project-standing">
        <CardContent className="flex flex-col gap-4">
          <h2 className="font-heading text-xl font-semibold">Where things stand</h2>
          <dl className="client-project-standing-list grid gap-3 sm:grid-cols-2">
            <div>
              <dt className="text-sm text-muted-foreground">Right now</dt>
              <dd className="font-medium">{now}</dd>
            </div>
            {next ? (
              <div>
                <dt className="text-sm text-muted-foreground">Up next</dt>
                <dd className="font-medium">{next}</dd>
              </div>
            ) : null}
          </dl>
        </CardContent>
      </Card>

      <ClientUpdateList updates={project.updates} workspaceName={project.workspace.name} latest={LATEST_UPDATES}>
        {project.updates.length > LATEST_UPDATES ? (
          <p className="mt-3 text-sm">
            <GoToTab tab="updates">See all updates ({project.updates.length})</GoToTab>
          </p>
        ) : null}
      </ClientUpdateList>

      <ClientStageList stages={project.stages} />
    </div>
  );
}
