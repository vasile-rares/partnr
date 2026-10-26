import { PencilIcon } from "lucide-react";
import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { DeliverableList } from "@/components/deliverables/DeliverableList";
import { FileList } from "@/components/files/FileList";
import { ArchiveProjectButton } from "@/components/projects/ArchiveProjectButton";
import { ProjectApprovalsCard } from "@/components/projects/ProjectApprovalsCard";
import { ProjectForm } from "@/components/projects/ProjectForm";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { ProjectTabs } from "@/components/projects/ProjectTabs";
import { ProjectTimeline } from "@/components/projects/ProjectTimeline";
import { Page } from "@/components/shell/Page";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { UpdateList } from "@/components/updates/UpdateList";
import { requireUser } from "@/lib/auth/session";
import { getDeliverables } from "@/lib/deliverables/server";
import { getProjectFiles } from "@/lib/files/server";
import { getProject } from "@/lib/projects/server";
import { getProjectUpdates } from "@/lib/updates/server";
import { getWorkspace } from "@/lib/workspaces/server";

// Archived ones are loaded too (the owner restores them from the list) but do not count.
const isLive = (item: { archivedAtUtc: string | null }) => item.archivedAtUtc === null;

export async function generateMetadata({ params }: PageProps<"/w/[workspaceId]/projects/[projectId]">): Promise<Metadata> {
  const { workspaceId, projectId } = await params;
  const workspace = await getWorkspace(workspaceId);
  const project = workspace ? await getProject(workspace.id, projectId) : null;
  return { title: project ? project.name : "Not found" };
}

export default async function ProjectPage({ params }: PageProps<"/w/[workspaceId]/projects/[projectId]">) {
  await requireUser();
  const { workspaceId, projectId } = await params;
  const workspace = await getWorkspace(workspaceId);
  if (!workspace) {
    notFound();
  }
  const project = await getProject(workspace.id, projectId);
  if (!project) {
    notFound();
  }
  const archived = project.archivedAtUtc !== null;
  const isOwner = workspace.role === "Owner";
  const [deliverables, updates, files] = await Promise.all([
    getDeliverables(workspace.id, project.id),
    getProjectUpdates(workspace.id, project.id),
    getProjectFiles(workspace.id, project.id),
  ]);

  return (
    <Page crumbs={[{ label: "Projects", href: `/w/${workspace.id}/projects` }, { label: project.name }]}>
      {/* The panel that edits the project wraps the page, so the button below and the overview's "Set dates" both open it. */}
      <Sheet>
        <div className="mb-2 flex flex-wrap items-start justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="font-heading text-3xl font-semibold">{project.name}</h1>
            <ProjectStatusBadge status={project.status} />
          </div>
          <SheetTrigger render={<Button variant="outline" />}>
            <PencilIcon data-icon="inline-start" />
            Edit project
          </SheetTrigger>
        </div>
        <p className="mb-6 text-muted-foreground">
          Client:{" "}
          <Link href={`/w/${workspace.id}/clients/${project.client.id}`} className="font-medium text-foreground hover:underline">
            {project.client.displayName}
          </Link>
          {project.status === "Planning" ? " · Not visible in their portal yet" : null}
        </p>

        {archived ? (
          <Alert className="mb-8 max-w-lg">
            <AlertDescription>
              This project is archived: it is hidden from the projects list and can&apos;t be changed until you restore it.
            </AlertDescription>
          </Alert>
        ) : null}

        <ProjectTimeline
          project={project}
          editable={archived ? undefined : { workspaceId: workspace.id, projectId: project.id }}
          datesAction={
            // Opens the project's edit panel: the timeline sits inside the page's <Sheet>.
            <SheetTrigger render={<Button type="button" variant="link" className="h-auto p-0 underline underline-offset-4" />}>
              Set dates
            </SheetTrigger>
          }
        />

        <ProjectTabs
          key={project.id}
          tabs={[
            { value: "overview", label: "Overview" },
            { value: "deliverables", label: "Deliverables & Files", count: deliverables.filter(isLive).length + files.filter(isLive).length },
          ]}
          panels={{
            overview: (
              <div className="grid items-start gap-8 lg:grid-cols-[minmax(0,32rem)_20rem]">
                <div>
                  <UpdateList workspaceId={workspace.id} project={project} updates={updates} isOwner={isOwner} />
                </div>
                <aside aria-label="Project summary" className="order-first flex flex-col gap-4 lg:order-none">
                  <ProjectApprovalsCard approvals={project.approvals} />
                </aside>
              </div>
            ),
            deliverables: (
              <div className="grid items-start gap-x-10 xl:grid-cols-2">
                <DeliverableList workspaceId={workspace.id} project={project} deliverables={deliverables} isOwner={isOwner} />
                <FileList workspaceId={workspace.id} project={project} files={files} isOwner={isOwner} />
              </div>
            ),
          }}
        />

        <SheetContent className="data-[side=right]:w-full data-[side=right]:sm:max-w-lg">
          <SheetHeader>
            <SheetTitle>Edit project</SheetTitle>
            <SheetDescription>Name, dates and status. Your client sees a project once it leaves Planning.</SheetDescription>
          </SheetHeader>
          <div className="min-h-0 flex-1 overflow-y-auto px-4 pb-6">
            <ProjectForm workspaceId={workspace.id} project={project} />

            <Separator className="my-8" />

            <section aria-labelledby="archive-title">
              <h2 id="archive-title" className="font-heading text-xl font-semibold">
                {archived ? "Restore project" : "Archive project"}
              </h2>
              {isOwner ? (
                <>
                  <p className="mb-4 mt-2 text-sm text-muted-foreground">
                    {archived
                      ? "Bring this project back to the list and make it editable again."
                      : "Hide this project from the list and from your client's portal. Nothing is deleted, and you can restore it at any time."}
                  </p>
                  <ArchiveProjectButton workspaceId={workspace.id} project={project} />
                </>
              ) : (
                <p className="mt-2 text-sm text-muted-foreground">Only the workspace owner can archive or restore projects.</p>
              )}
            </section>
          </div>
        </SheetContent>
      </Sheet>
    </Page>
  );
}
