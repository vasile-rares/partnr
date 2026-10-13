import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { AppHeader } from "@/components/AppHeader";
import { ClientForm } from "@/components/clients/ClientForm";
import { requireUser } from "@/lib/auth/session";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Add client" };

export default async function NewClientPage({ params }: PageProps<"/w/[workspaceId]/clients/new">) {
  await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }

  return (
    <>
      <AppHeader workspace={workspace} />
      <main className="mx-auto w-full max-w-3xl px-6 py-10">
        <Link href={`/w/${workspace.id}/clients`} className="text-sm text-muted-foreground hover:underline">
          ← Clients
        </Link>
        <h1 className="mb-8 mt-2 font-heading text-3xl font-semibold">Add client</h1>

        <ClientForm workspaceId={workspace.id} />
      </main>
    </>
  );
}
