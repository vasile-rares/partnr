import { ExternalLinkIcon } from "lucide-react";
import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { AppHeader } from "@/components/AppHeader";
import { ProjectProgress } from "@/components/projects/ProjectProgress";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { StageStatusBadge } from "@/components/projects/StageStatusBadge";
import { Card, CardContent } from "@/components/ui/card";
import { requireUser } from "@/lib/auth/session";
import { formatDate } from "@/lib/format";
import { getPortalProject } from "@/lib/portal/server";
import { summarize } from "@/lib/portal/summary";

export async function generateMetadata({ params }: PageProps<"/portal/projects/[projectId]">): Promise<Metadata> {
  const project = await getPortalProject((await params).projectId);
  return { title: project ? project.name : "Not found" };
}

export default async function PortalProjectPage({ params }: PageProps<"/portal/projects/[projectId]">) {
  await requireUser();
  const project = await getPortalProject((await params).projectId);
  if (!project) {
    notFound();
  }
  const { now, next } = summarize(project.status, project.stages);
  const dates = [
    project.startDate ? `Started ${formatDate(project.startDate)}` : null,
    project.dueDate ? `Due ${formatDate(project.dueDate)}` : null,
  ].filter(Boolean);

  return (
    <>
      <AppHeader />
      <main className="mx-auto w-full max-w-3xl px-6 py-10">
        <Link href="/portal" className="text-sm text-muted-foreground hover:underline">
          ← Your portal
        </Link>
        <div className="mb-2 mt-2 flex flex-wrap items-center gap-3">
          <h1 className="font-heading text-3xl font-semibold">{project.name}</h1>
          <ProjectStatusBadge status={project.status} />
        </div>
        <p className="text-muted-foreground">
          With {project.workspace.name}
          {dates.length > 0 ? ` · ${dates.join(" · ")}` : null}
        </p>
        {project.description ? <p className="mt-4 max-w-prose whitespace-pre-line">{project.description}</p> : null}

        <Card className="mt-8">
          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 className="font-heading text-xl font-semibold">Where things stand</h2>
              <ProjectProgress progress={project.progress} />
            </div>
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

        {project.deliverables.length > 0 ? (
          <section aria-labelledby="deliverables-title" className="mt-10">
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
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        {project.stages.length > 0 ? (
          <section aria-labelledby="stages-title" className="mt-10">
            <h2 id="stages-title" className="mb-4 font-heading text-xl font-semibold">
              Stages
            </h2>
            <ol className="flex flex-col divide-y rounded-lg border">
              {project.stages.map((stage) => (
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
      </main>
    </>
  );
}
