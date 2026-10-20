import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Page } from "@/components/shell/Page";
import { RenameWorkspaceForm } from "@/components/workspaces/RenameWorkspaceForm";
import { requireUser } from "@/lib/auth/session";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Workspace settings" };

export default async function WorkspaceSettingsPage({ params }: PageProps<"/w/[workspaceId]/settings">) {
  await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }

  return (
    <Page crumbs={[{ label: "Settings" }]} narrow>
      <h1 className="mb-8 font-heading text-3xl font-semibold">Workspace settings</h1>

      {workspace.role === "Owner" ? (
        <RenameWorkspaceForm workspaceId={workspace.id} name={workspace.name} />
      ) : (
        <dl className="max-w-sm">
          <dt className="text-sm font-medium">Workspace name</dt>
          <dd className="mt-1">{workspace.name}</dd>
          <dd className="mt-3 text-sm text-muted-foreground">Only the owner of this workspace can rename it.</dd>
        </dl>
      )}
    </Page>
  );
}
