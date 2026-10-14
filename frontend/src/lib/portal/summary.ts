import type { Stage, ProjectStatus } from "@/lib/projects/types";

/** The two things a client wants to know first: where the project is, and what comes after. */
export type ProjectSummary = {
  /** Where things stand right now. */
  now: string;
  /** What comes next, or null when nothing does. */
  next: string | null;
};

const names = (stages: Stage[]) => stages.map((s) => s.name).join(" and ");

/** Puts the project's status and stages into words for the client. The status wins over the stages: a paused or finished project says so. */
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

  const inProgress = stages.filter((s) => s.status === "InProgress");
  const upNext = stages.find((s) => s.status === "ToDo");

  if (inProgress.length === 0 && !upNext) {
    return { now: "Every stage is done.", next: null };
  }

  return {
    now: inProgress.length > 0 ? `Working on ${names(inProgress)}.` : "Nothing is being worked on at this moment.",
    next: upNext ? upNext.name : null,
  };
}
