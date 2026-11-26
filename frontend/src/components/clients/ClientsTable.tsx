"use client";

import { SearchIcon } from "lucide-react";
import Link from "next/link";
import { useMemo, useState, type ReactNode } from "react";
import { ClientAvatar } from "@/components/clients/ClientAvatar";
import { PortalStatusBadge } from "@/components/clients/PortalStatusBadge";
import { DataGrid, DataGridContainer, dataGridFeatures, type DataGridFeatures } from "@/components/reui/data-grid/data-grid";
import { DataGridColumnHeader } from "@/components/reui/data-grid/data-grid-column-header";
import { DataGridPagination } from "@/components/reui/data-grid/data-grid-pagination";
import { DataGridScrollArea } from "@/components/reui/data-grid/data-grid-scroll-area";
import { DataGridTable } from "@/components/reui/data-grid/data-grid-table";
import { Badge } from "@/components/reui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { PortalStatus } from "@/lib/clients/types";
import { useTable, type ColumnDef } from "@tanstack/react-table";

export type ClientRow = {
  id: string;
  displayName: string;
  email: string;
  company: string | null;
  archived: boolean;
  portalStatus: PortalStatus;
  /** Projects that are not archived or completed. */
  activeProjects: number;
};

type Scope = "active" | "archived";

const PAGE_SIZE = 10;

type ClientsTableProps = {
  workspaceId: string;
  clients: ClientRow[];
  initialScope: Scope;
  /** Shown in the empty state of a workspace without clients (the page's own "Add client" dialog). */
  addClient: ReactNode;
};

/** Search and the Active / Archived switch only filter what the page already loaded; the scope is kept in the address. */
export function ClientsTable({ workspaceId, clients, initialScope, addClient }: ClientsTableProps) {
  const [scope, setScope] = useState<Scope>(initialScope);
  const [search, setSearch] = useState("");

  const activeCount = clients.filter((client) => !client.archived).length;
  const archivedCount = clients.length - activeCount;
  const needle = search.trim().toLowerCase();
  const visible = clients.filter(
    (client) =>
      client.archived === (scope === "archived") &&
      (needle === "" || [client.displayName, client.email, client.company ?? ""].some((text) => text.toLowerCase().includes(needle))),
  );

  function selectScope(next: Scope) {
    setScope(next);
    window.history.replaceState(null, "", next === "archived" ? "?archived=1" : window.location.pathname);
  }

  if (clients.length === 0) {
    return (
      <div className="clients-empty flex max-w-prose flex-col items-start gap-4 rounded-lg border border-dashed p-8">
        <p className="text-muted-foreground">No clients yet. Add your first client to start working on their projects.</p>
        {addClient}
      </div>
    );
  }

  return (
    <div className="clients-list flex flex-col gap-4">
      <div className="clients-toolbar flex flex-wrap items-center justify-between gap-3 border-b">
        <Tabs value={scope} onValueChange={(value) => selectScope(value as Scope)}>
          <TabsList variant="line" className="clients-tabs h-10 gap-4 p-0">
            <TabsTrigger value="active" className="flex-none px-0.5 text-base">
              Active
              <span className="text-xs text-muted-foreground tabular-nums">{activeCount}</span>
            </TabsTrigger>
            <TabsTrigger value="archived" className="flex-none px-0.5 text-base">
              Archived
              <span className="text-xs text-muted-foreground tabular-nums">{archivedCount}</span>
            </TabsTrigger>
          </TabsList>
        </Tabs>
        <div className="clients-search relative mb-2 w-full sm:w-64">
          <SearchIcon className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
          <Input
            type="search"
            aria-label="Search clients"
            placeholder="Search clients"
            className="pl-8"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>
      </div>

      {visible.length === 0 ? (
        <div className="clients-no-results flex max-w-prose flex-col items-start gap-3 py-4">
          {needle !== "" ? (
            <>
              <p className="text-muted-foreground">No clients match “{search.trim()}”.</p>
              <Button variant="outline" onClick={() => setSearch("")}>
                Clear search
              </Button>
            </>
          ) : (
            <p className="text-muted-foreground">
              {scope === "archived"
                ? "No archived clients."
                : "No active clients. Add a client to start working on their projects, or open the archived ones."}
            </p>
          )}
        </div>
      ) : (
        <ClientsGrid workspaceId={workspaceId} rows={visible} />
      )}
    </div>
  );
}

function ClientsGrid({ workspaceId, rows }: { workspaceId: string; rows: ClientRow[] }) {
  const columns = useMemo<ColumnDef<DataGridFeatures, ClientRow>[]>(
    () => [
      {
        id: "client",
        accessorFn: (client) => client.displayName,
        header: ({ column }) => <DataGridColumnHeader column={column} title="Client" />,
        cell: ({ row }) => {
          const client = row.original;
          return (
            <div className="clients-table-person flex items-center gap-3">
              <ClientAvatar name={client.displayName} />
              <div className="clients-table-person-info min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <Link href={`/w/${workspaceId}/clients/${client.id}`} className="font-medium hover:underline">
                    {client.displayName}
                  </Link>
                  {client.archived ? <Badge variant="secondary">Archived</Badge> : null}
                </div>
                <p className="break-all text-sm text-muted-foreground">
                  {client.displayName === client.email ? null : client.email}
                  {client.company ? `${client.displayName === client.email ? "" : " · "}${client.company}` : null}
                </p>
              </div>
            </div>
          );
        },
        meta: { headerTitle: "Client" },
      },
      {
        id: "projects",
        accessorFn: (client) => client.activeProjects,
        header: ({ column }) => <DataGridColumnHeader column={column} title="Projects" />,
        cell: ({ row }) => (row.original.activeProjects > 0 ? row.original.activeProjects : "—"),
        meta: { headerTitle: "Projects", headerClassName: "hidden sm:table-cell", cellClassName: "hidden sm:table-cell" },
      },
      {
        id: "portal",
        accessorFn: (client) => client.portalStatus,
        header: ({ column }) => <DataGridColumnHeader column={column} title="Portal" />,
        cell: ({ row }) => <PortalStatusBadge status={row.original.portalStatus} />,
        meta: { headerTitle: "Portal" },
      },
    ],
    [workspaceId],
  );

  const table = useTable({
    features: dataGridFeatures,
    columns,
    data: rows,
    getRowId: (client) => client.id,
    initialState: { pagination: { pageIndex: 0, pageSize: PAGE_SIZE } },
  });

  return (
    <DataGrid table={table} recordCount={rows.length} tableLayout={{ width: "auto" }}>
      <DataGridContainer className="clients-grid">
        <DataGridScrollArea>
          <DataGridTable />
        </DataGridScrollArea>
      </DataGridContainer>
      {rows.length > PAGE_SIZE ? <DataGridPagination className="clients-pagination" sizes={[10, 25, 50]} /> : null}
    </DataGrid>
  );
}
