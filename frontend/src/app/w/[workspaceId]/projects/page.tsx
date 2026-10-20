import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ProjectsTable } from "@/components/projects/ProjectsTable";
import { Page } from "@/components/shell/Page";
import { buttonVariants } from "@/components/ui/button";
import { requireUser } from "@/lib/auth/session";
import { getProjects } from "@/lib/projects/server";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Projects" };

export default async function ProjectsPage({ params, searchParams }: PageProps<"/w/[workspaceId]/projects">) {
  await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const showArchived = (await searchParams).archived === "1";
  const projects = await getProjects(workspace.id, { includeArchived: showArchived });
  const base = `/w/${workspace.id}/projects`;

  return (
    <Page crumbs={[{ label: "Projects" }]}>
      <div className="mb-8 flex flex-wrap items-center justify-between gap-4">
        <h1 className="font-heading text-3xl font-semibold">Projects</h1>
        <Link href={`${base}/new`} className={buttonVariants()}>
          New project
        </Link>
      </div>

      <p className="mb-4 text-sm">
        <Link href={showArchived ? base : `${base}?archived=1`} className="text-muted-foreground hover:underline">
          {showArchived ? "Hide archived projects" : "Show archived projects"}
        </Link>
      </p>

      {projects.length === 0 ? (
        <p className="max-w-prose text-muted-foreground">
          {showArchived
            ? "No projects yet. Start one for a client, and they can follow it in their portal."
            : "No active projects. Start one for a client, or show the archived ones."}
        </p>
      ) : (
        <ProjectsTable workspaceId={workspace.id} projects={projects} />
      )}
    </Page>
  );
}
