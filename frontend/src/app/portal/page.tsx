import type { Metadata } from "next";
import Link from "next/link";
import { AppHeader } from "@/components/AppHeader";
import { ProjectProgress } from "@/components/projects/ProjectProgress";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { requireUser } from "@/lib/auth/session";
import { formatDate } from "@/lib/format";
import { getPortalProjects, getPortalWorkspaces } from "@/lib/portal/server";

export const metadata: Metadata = { title: "Your portal" };

// For each freelancer or agency the client works with, the projects shared with them.
export default async function PortalPage() {
  await requireUser();
  const [workspaces, projects] = await Promise.all([getPortalWorkspaces(), getPortalProjects()]);

  return (
    <>
      <AppHeader />
      <main className="mx-auto w-full max-w-3xl px-6 py-10">
        <h1 className="font-heading text-3xl font-semibold">Your portal</h1>

        {workspaces.length === 0 ? (
          <p className="mt-6 max-w-prose text-muted-foreground">
            You are not connected to anyone yet. When a freelancer or agency invites you, they will show up here.
          </p>
        ) : (
          <>
            <p className="mt-3 max-w-prose text-muted-foreground">
              Open a project to see where it stands and what comes next.
            </p>
            <div className="mt-8 flex flex-col gap-10">
              {workspaces.map((workspace) => {
                const shared = projects.filter((project) => project.workspace.id === workspace.id);

                return (
                  <section key={workspace.id} aria-labelledby={`workspace-${workspace.id}`}>
                    <h2 id={`workspace-${workspace.id}`} className="mb-3 font-heading text-xl font-semibold">
                      {workspace.name}
                    </h2>
                    {shared.length === 0 ? (
                      <p className="text-sm text-muted-foreground">
                        Nothing has been shared with you yet. Projects show up here as soon as they are ready.
                      </p>
                    ) : (
                      <ul className="divide-y rounded-lg border">
                        {shared.map((project) => (
                          <li key={project.id}>
                            <Link
                              href={`/portal/projects/${project.id}`}
                              className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3 p-4 hover:bg-muted/50"
                            >
                              <div className="min-w-0">
                                <p className="font-medium">{project.name}</p>
                                <p className="mt-1 text-sm text-muted-foreground">
                                  {project.dueDate ? `Due ${formatDate(project.dueDate)}` : "No due date"}
                                </p>
                              </div>
                              <div className="flex items-center gap-4">
                                <ProjectProgress progress={project.progress} />
                                <ProjectStatusBadge status={project.status} />
                              </div>
                            </Link>
                          </li>
                        ))}
                      </ul>
                    )}
                  </section>
                );
              })}
            </div>
          </>
        )}
      </main>
    </>
  );
}
