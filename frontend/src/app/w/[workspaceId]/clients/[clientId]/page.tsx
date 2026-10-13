import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { AppHeader } from "@/components/AppHeader";
import { ArchiveClientButton } from "@/components/clients/ArchiveClientButton";
import { ClientForm } from "@/components/clients/ClientForm";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { requireUser } from "@/lib/auth/session";
import { getClient } from "@/lib/clients/server";
import { getWorkspace } from "@/lib/workspaces/server";

export async function generateMetadata({ params }: PageProps<"/w/[workspaceId]/clients/[clientId]">): Promise<Metadata> {
  const { workspaceId, clientId } = await params;
  const workspace = await getWorkspace(workspaceId);
  const client = workspace ? await getClient(workspace.id, clientId) : null;
  return { title: client?.name ?? "Not found" };
}

export default async function ClientPage({ params }: PageProps<"/w/[workspaceId]/clients/[clientId]">) {
  await requireUser();
  const { workspaceId, clientId } = await params;
  const workspace = await getWorkspace(workspaceId);
  if (!workspace) {
    notFound();
  }
  // A client of another workspace looks the same as one that does not exist (the API answers 404 for both).
  const client = await getClient(workspace.id, clientId);
  if (!client) {
    notFound();
  }

  return (
    <>
      <AppHeader workspace={workspace} />
      <main className="mx-auto w-full max-w-3xl px-6 py-10">
        <Link href={`/w/${workspace.id}/clients`} className="text-sm text-muted-foreground hover:underline">
          ← Clients
        </Link>
        <h1 className="mb-8 mt-2 font-heading text-3xl font-semibold">{client.name}</h1>

        {client.archivedAtUtc ? (
          <Alert className="mb-8 max-w-lg">
            <AlertDescription>
              This client is archived. It is hidden from the clients list until you restore it.
            </AlertDescription>
          </Alert>
        ) : null}

        <ClientForm workspaceId={workspace.id} client={client} />

        <section aria-labelledby="archive-title" className="mt-12 max-w-lg border-t pt-8">
          <h2 id="archive-title" className="font-heading text-xl font-semibold">
            {client.archivedAtUtc ? "Restore client" : "Archive client"}
          </h2>
          {/* The API enforces this too; hiding the button only spares a member an error they cannot fix. */}
          {workspace.role === "Owner" ? (
            <>
              <p className="mb-4 mt-2 text-sm text-muted-foreground">
                {client.archivedAtUtc
                  ? "Bring this client back to the clients list."
                  : "Hide this client from the list. Nothing is deleted, and you can restore it at any time."}
              </p>
              <ArchiveClientButton workspaceId={workspace.id} client={client} />
            </>
          ) : (
            <p className="mt-2 text-sm text-muted-foreground">
              Only the workspace owner can archive or restore clients.
            </p>
          )}
        </section>
      </main>
    </>
  );
}
