import { orderByDate } from "@/lib/projects/timeline";
import type { Stage, ProjectStatus } from "@/lib/projects/types";

export type ProjectSummary = {
  now: string;
  next: string | null;
};

const names = (stages: Stage[]) => stages.map((s) => s.name).join(" and ");

/** The status wins over the stages: a paused or finished project says so. */
export function summarize(status: ProjectStatus, stages: Stage[]): ProjectSummary {
  if (status === "Completed") {
    return { now: "This project is complete.", next: null };
  }
  if (status === "OnHold") {
    return { now: "This project is on hold for now.", next: null };
  }
  if (stages.length === 0) {
    return { now: "The stages of this project haven't been set up yet.", next: null };
  }

  const ordered = orderByDate(stages);
  const inProgress = ordered.filter((s) => s.status === "InProgress");
  const upNext = ordered.find((s) => s.status === "ToDo");

  if (inProgress.length === 0 && !upNext) {
    return { now: "Every stage is done.", next: null };
  }

  return {
    now: inProgress.length > 0 ? `Working on ${names(inProgress)}.` : "Nothing is being worked on at this moment.",
    next: upNext ? upNext.name : null,
  };
}
