import { ExternalLinkIcon } from "lucide-react";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PortalFileList } from "@/components/files/PortalFileList";
import { PortalApprovalPanel } from "@/components/portal/PortalApprovalPanel";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { GoToTab, ProjectTabs, type ProjectTabItem } from "@/components/projects/ProjectTabs";
import { ProjectTimeline } from "@/components/projects/ProjectTimeline";
import { StageStatusBadge } from "@/components/projects/StageStatusBadge";
import { Page } from "@/components/shell/Page";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Card, CardContent } from "@/components/ui/card";
import { PortalUpdateList } from "@/components/updates/PortalUpdateList";
import { requireUser } from "@/lib/auth/session";
import { orderByDate } from "@/lib/projects/timeline";
import { formatDate } from "@/lib/format";
import { getPortalProject } from "@/lib/portal/server";
import { summarize } from "@/lib/portal/summary";

type Params = PageProps<"/portal/[workspaceId]/projects/[projectId]">["params"];

const LATEST_UPDATES = 3;

// The firm in the URL has to be the project's own: a project opened under another firm's address is not found.
async function findProject(params: Params) {
  const { workspaceId, projectId } = await params;
  const project = await getPortalProject(projectId);
  return project?.workspace.id === workspaceId ? project : null;
}

export async function generateMetadata({ params }: PageProps<"/portal/[workspaceId]/projects/[projectId]">): Promise<Metadata> {
  const project = await findProject(params);
  return { title: project ? project.name : "Not found" };
}

export default async function PortalProjectPage({ params }: PageProps<"/portal/[workspaceId]/projects/[projectId]">) {
  await requireUser();
  const project = await findProject(params);
  if (!project) {
    notFound();
  }
  const { now, next } = summarize(project.status, project.stages);

  // A tab with nothing in it is not offered; Updates stays because its empty state says what to expect.
  const tabs: ProjectTabItem[] = [{ value: "overview", label: "Overview" }];
  if (project.deliverables.length > 0) {
    tabs.push({ value: "deliverables", label: "Deliverables", count: project.awaitingApproval, highlight: true });
  }
  tabs.push({ value: "updates", label: "Updates", count: project.updates.length });
  if (project.files.length > 0) {
    tabs.push({ value: "files", label: "Files", count: project.files.length });
  }

  return (
    <Page crumbs={[{ label: "Projects", href: `/portal/${project.workspace.id}/projects` }, { label: project.name }]} narrow>
      <div className="mb-2 flex flex-wrap items-center gap-3">
        <h1 className="font-heading text-3xl font-semibold">{project.name}</h1>
        <ProjectStatusBadge status={project.status} />
      </div>
      <p className="mb-6 text-muted-foreground">With {project.workspace.name}</p>

      <ProjectTimeline project={project} />

      <ProjectTabs
        key={project.id}
        tabs={tabs}
        panels={{
          overview: (
            <div className="flex flex-col gap-10">
              {project.description ? <p className="max-w-prose whitespace-pre-line">{project.description}</p> : null}

              {project.awaitingApproval > 0 ? (
                // role="status": worth announcing politely, but not an error, so not the assertive default of Alert.
                <Alert role="status">
                  <AlertTitle>Waiting for you</AlertTitle>
                  <AlertDescription>
                    {project.awaitingApproval === 1
                      ? "1 deliverable needs your approval."
                      : `${project.awaitingApproval} deliverables need your approval.`}{" "}
                    <GoToTab tab="deliverables">See what to approve</GoToTab>
                  </AlertDescription>
                </Alert>
              ) : null}

              <Card>
                <CardContent className="flex flex-col gap-4">
                  <h2 className="font-heading text-xl font-semibold">Where things stand</h2>
                  <dl className="grid gap-3 sm:grid-cols-2">
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

              <PortalUpdateList updates={project.updates} workspaceName={project.workspace.name} latest={LATEST_UPDATES}>
                {project.updates.length > LATEST_UPDATES ? (
                  <p className="mt-3 text-sm">
                    <GoToTab tab="updates">See all updates ({project.updates.length})</GoToTab>
                  </p>
                ) : null}
              </PortalUpdateList>

              {project.stages.length > 0 ? (
                <section aria-labelledby="stages-title">
                  <h2 id="stages-title" className="mb-4 font-heading text-xl font-semibold">
                    Stages
                  </h2>
                  <ol className="flex flex-col divide-y rounded-lg border">
                    {orderByDate(project.stages).map((stage) => (
                      <li key={stage.id} className="p-4">
                        <div className="flex flex-wrap items-center justify-between gap-3">
                          <p className="font-medium">{stage.name}</p>
                          <StageStatusBadge status={stage.status} />
                        </div>
                        {stage.description ? <p className="mt-1 text-sm text-muted-foreground">{stage.description}</p> : null}
                        {stage.dueDate ? (
                          <p className="mt-2 text-sm text-muted-foreground">Due {formatDate(stage.dueDate)}</p>
                        ) : null}
                      </li>
                    ))}
                  </ol>
                </section>
              ) : null}
            </div>
          ),
          deliverables: (
            <section aria-labelledby="deliverables-title">
              <h2 id="deliverables-title" className="mb-4 font-heading text-xl font-semibold">
                Deliverables
              </h2>
              <ul className="flex flex-col divide-y rounded-lg border">
                {project.deliverables.map((deliverable) => (
                  <li key={deliverable.id} className="p-4">
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
                    {deliverable.stageName ? (
                      <p className="mt-2 text-sm text-muted-foreground">Stage: {deliverable.stageName}</p>
                    ) : null}
                    <PortalApprovalPanel projectId={project.id} deliverable={deliverable} workspaceName={project.workspace.name} />
                  </li>
                ))}
              </ul>
            </section>
          ),
          updates: <PortalUpdateList updates={project.updates} workspaceName={project.workspace.name} />,
          files: <PortalFileList projectId={project.id} files={project.files} />,
        }}
      />
    </Page>
  );
}
