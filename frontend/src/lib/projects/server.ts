import "server-only";

import { cache } from "react";
import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import type { ClientProject, ClientProjectSummary, Project } from "@/lib/projects/types";
import { isUuid } from "@/lib/uuid";

// The workspace id must already be checked (via `getWorkspace`), which also rejects ids that are not UUIDs. Project and
// client ids come straight from the URL, so they are checked here.
//
// Members and clients call the same routes and the API answers each in its own shape, so a function exists per shape
// and both go through the same two fetchers.

async function fetchProjects<T>(workspaceId: string, query = ""): Promise<T[]> {
  const response = await serverFetch(`/api/workspaces/${workspaceId}/projects${query}`);

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the projects (HTTP ${response.status}).`);
  }

  return (await response.json()) as T[];
}

/** Null when there is none like it for this caller (someone else's, a draft a client may not see and a missing one look the same). */
async function fetchProject<T>(workspaceId: string, projectId: string): Promise<T | null> {
  if (!isUuid(projectId)) {
    return null;
  }

  const response = await serverFetch(`/api/workspaces/${workspaceId}/projects/${projectId}`);

  if (response.status === 404) {
    return null;
  }
  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the project (HTTP ${response.status}).`);
  }

  return (await response.json()) as T;
}

type ProjectFilter = { includeArchived?: boolean; clientId?: string };

export async function getProjects(workspaceId: string, { includeArchived = false, clientId }: ProjectFilter = {}): Promise<Project[]> {
  const query = new URLSearchParams();
  if (includeArchived) {
    query.set("includeArchived", "true");
  }
  if (clientId && isUuid(clientId)) {
    query.set("clientId", clientId);
  }

  return fetchProjects<Project>(workspaceId, query.size > 0 ? `?${query}` : "");
}

export const getProject = cache((workspaceId: string, projectId: string) => fetchProject<Project>(workspaceId, projectId));

/** The projects this workspace shared with the signed-in client. */
export const getClientProjectSummaries = cache((workspaceId: string) => fetchProjects<ClientProjectSummary>(workspaceId));

export const getClientProject = cache((workspaceId: string, projectId: string) => fetchProject<ClientProject>(workspaceId, projectId));

/** Every shared project with its details. A client has few projects with one workspace, so they are loaded side by side instead of adding an endpoint. */
export async function getClientProjects(workspaceId: string): Promise<ClientProject[]> {
  const summaries = await getClientProjectSummaries(workspaceId);
  const projects = await Promise.all(summaries.map((summary) => getClientProject(workspaceId, summary.id)));

  return projects.filter((project): project is ClientProject => project !== null);
}
