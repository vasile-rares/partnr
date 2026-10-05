import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { AppHeader } from "@/components/AppHeader";
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
    <>
      <AppHeader workspace={workspace} />
      <main className="mx-auto w-full max-w-3xl px-6 py-10">
        <h1 className="mb-8 font-heading text-3xl font-semibold">Workspace settings</h1>

        {/* The API enforces this too; hiding the form only spares a member an error they cannot fix. */}
        {workspace.role === "Owner" ? (
          <RenameWorkspaceForm workspaceId={workspace.id} name={workspace.name} />
        ) : (
          <dl className="max-w-sm">
            <dt className="text-sm font-medium">Workspace name</dt>
            <dd className="mt-1">{workspace.name}</dd>
            <dd className="mt-3 text-sm text-muted-foreground">Only the owner of this workspace can rename it.</dd>
          </dl>
        )}
      </main>
    </>
  );
}
