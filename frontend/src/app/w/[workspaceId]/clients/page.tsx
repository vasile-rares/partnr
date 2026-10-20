import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { PortalStatusBadge } from "@/components/clients/PortalStatusBadge";
import { Page } from "@/components/shell/Page";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { requireUser } from "@/lib/auth/session";
import { getClients } from "@/lib/clients/server";
import { displayName } from "@/lib/clients/types";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Clients" };

export default async function ClientsPage({ params, searchParams }: PageProps<"/w/[workspaceId]/clients">) {
  await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const showArchived = (await searchParams).archived === "1";
  const clients = await getClients(workspace.id, showArchived);
  const base = `/w/${workspace.id}/clients`;

  return (
    <Page crumbs={[{ label: "Clients" }]}>
      <div className="mb-8 flex flex-wrap items-center justify-between gap-4">
        <h1 className="font-heading text-3xl font-semibold">Clients</h1>
        <Link href={`${base}/new`} className={buttonVariants()}>
          Add client
        </Link>
      </div>

      <p className="mb-4 text-sm">
        <Link href={showArchived ? base : `${base}?archived=1`} className="text-muted-foreground hover:underline">
          {showArchived ? "Hide archived clients" : "Show archived clients"}
        </Link>
      </p>

      {clients.length === 0 ? (
        <p className="max-w-prose text-muted-foreground">
          {showArchived
            ? "No clients yet. Add your first client to start working on their projects."
            : "No active clients. Add a client to start working on their projects, or show the archived ones."}
        </p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Company</TableHead>
              <TableHead>Email</TableHead>
              <TableHead>Portal</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {clients.map((client) => (
              <TableRow key={client.id}>
                <TableCell className="font-medium">
                  <Link href={`${base}/${client.id}`} className="hover:underline">
                    {displayName(client)}
                  </Link>
                  {client.archivedAtUtc ? (
                    <Badge variant="secondary" className="ml-2">
                      Archived
                    </Badge>
                  ) : null}
                </TableCell>
                <TableCell>{client.company ?? "—"}</TableCell>
                <TableCell>{client.email}</TableCell>
                <TableCell>
                  <PortalStatusBadge status={client.portal.status} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </Page>
  );
}
