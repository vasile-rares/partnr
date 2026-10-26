import { daysUntil } from "@/lib/format";
import type { Project } from "@/lib/projects/types";

export const UPCOMING_DAYS = 14;

export type Deadline = {
  key: string;
  date: string;
  daysLeft: number;
  /** The stage or the project that is due. */
  title: string;
  project: { id: string; name: string };
  clientName: string;
  kind: "project" | "stage";
};

/** Open work due within the next two weeks, plus everything already late, soonest first. */
export function upcomingDeadlines(projects: Project[]): Deadline[] {
  const deadlines: Deadline[] = [];

  for (const project of projects) {
    if (project.status === "Completed") {
      continue;
    }
    const base = { project: { id: project.id, name: project.name }, clientName: project.client.displayName };

    if (project.dueDate) {
      deadlines.push({ ...base, key: project.id, date: project.dueDate, daysLeft: daysUntil(project.dueDate), title: project.name, kind: "project" });
    }
    for (const stage of project.stages) {
      if (stage.dueDate && stage.status !== "Done") {
        deadlines.push({ ...base, key: stage.id, date: stage.dueDate, daysLeft: daysUntil(stage.dueDate), title: stage.name, kind: "stage" });
      }
    }
  }

  return deadlines
    .filter((deadline) => deadline.daysLeft <= UPCOMING_DAYS)
    .sort((a, b) => a.date.localeCompare(b.date) || a.title.localeCompare(b.title));
}

/** Active and on-hold projects, the ones with a date first. */
export function openProjects(projects: Project[]): Project[] {
  return projects
    .filter((project) => project.status === "Active" || project.status === "OnHold")
    .sort((a, b) => (a.dueDate ?? "9999-12-31").localeCompare(b.dueDate ?? "9999-12-31") || a.name.localeCompare(b.name));
}
