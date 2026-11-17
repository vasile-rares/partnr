import Link from "next/link";
import { ClientAvatar } from "@/components/clients/ClientAvatar";
import { PortalStatusBadge } from "@/components/clients/PortalStatusBadge";
import { DividedList } from "@/components/layout/DividedList";
import { Section } from "@/components/layout/Section";
import type { PortalStatus } from "@/lib/clients/types";

export type WaitingClient = { id: string; displayName: string; portalStatus: PortalStatus };

export function WaitingOnClients({ workspaceId, clients }: { workspaceId: string; clients: WaitingClient[] }) {
  return (
    <Section title="Waiting on clients" className="waiting-on-clients">
      {clients.length === 0 ? (
        <p className="text-sm text-muted-foreground">Every client you invited has joined.</p>
      ) : (
        <>
          <p className="mb-3 text-sm text-muted-foreground">Invited, but not in their portal yet. An expired invitation needs a new one.</p>
          <DividedList className="waiting-on-clients-list">
            {clients.map((client) => (
              <li key={client.id} className="waiting-on-clients-item flex items-center justify-between gap-3 p-3">
                <div className="waiting-on-clients-person flex min-w-0 items-center gap-3">
                  <ClientAvatar name={client.displayName} className="size-8" />
                  <Link href={`/w/${workspaceId}/clients/${client.id}`} className="truncate font-medium hover:underline">
                    {client.displayName}
                  </Link>
                </div>
                <PortalStatusBadge status={client.portalStatus} />
              </li>
            ))}
          </DividedList>
        </>
      )}
    </Section>
  );
}
