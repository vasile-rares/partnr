import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { AppHeader } from "@/components/AppHeader";
import { ArchiveProjectButton } from "@/components/projects/ArchiveProjectButton";
import { ProjectForm } from "@/components/projects/ProjectForm";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { requireUser } from "@/lib/auth/session";
import { getProject } from "@/lib/projects/server";
import { getWorkspace } from "@/lib/workspaces/server";

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
  // A project of another workspace looks the same as one that does not exist (the API answers 404 for both).
  const project = await getProject(workspace.id, projectId);
  if (!project) {
    notFound();
  }
  const archived = project.archivedAtUtc !== null;

  return (
    <>
      <AppHeader workspace={workspace} />
      <main className="mx-auto w-full max-w-3xl px-6 py-10">
        <Link href={`/w/${workspace.id}/projects`} className="text-sm text-muted-foreground hover:underline">
          ← Projects
        </Link>
        <div className="mb-2 mt-2 flex flex-wrap items-center gap-3">
          <h1 className="font-heading text-3xl font-semibold">{project.name}</h1>
          <ProjectStatusBadge status={project.status} />
        </div>
        <p className="mb-8 text-muted-foreground">
          For{" "}
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

        <ProjectForm workspaceId={workspace.id} project={project} />

        <section aria-labelledby="archive-title" className="mt-12 max-w-lg border-t pt-8">
          <h2 id="archive-title" className="font-heading text-xl font-semibold">
            {archived ? "Restore project" : "Archive project"}
          </h2>
          {/* The API enforces this too; hiding the button only spares a member an error they cannot fix. */}
          {workspace.role === "Owner" ? (
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
      </main>
    </>
  );
}
