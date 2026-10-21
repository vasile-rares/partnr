"use client";

import { SearchIcon } from "lucide-react";
import Link from "next/link";
import { useState, type ReactNode } from "react";
import { ClientAvatar } from "@/components/clients/ClientAvatar";
import { PortalStatusBadge } from "@/components/clients/PortalStatusBadge";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { PortalStatus } from "@/lib/clients/types";

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
      <div className="flex max-w-prose flex-col items-start gap-4 rounded-lg border border-dashed p-8">
        <p className="text-muted-foreground">No clients yet. Add your first client to start working on their projects.</p>
        {addClient}
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b">
        <Tabs value={scope} onValueChange={(value) => selectScope(value as Scope)}>
          <TabsList variant="line" className="h-10 gap-4 p-0">
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
        <div className="relative mb-2 w-full sm:w-64">
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
        <div className="flex max-w-prose flex-col items-start gap-3 py-4">
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
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Client</TableHead>
              <TableHead className="hidden sm:table-cell">Projects</TableHead>
              <TableHead>Portal</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {visible.map((client) => (
              <TableRow key={client.id}>
                <TableCell>
                  <div className="flex items-center gap-3">
                    <ClientAvatar name={client.displayName} />
                    <div className="min-w-0">
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
                </TableCell>
                <TableCell className="hidden sm:table-cell">{client.activeProjects > 0 ? client.activeProjects : "—"}</TableCell>
                <TableCell>
                  <PortalStatusBadge status={client.portalStatus} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
