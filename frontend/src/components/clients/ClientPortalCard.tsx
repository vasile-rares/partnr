import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { displayName, type Client } from "@/lib/clients/types";
import { formatDateTimeUtc } from "@/lib/format";
import { ClientPortalActions } from "./ClientPortalActions";
import { PortalStatusBadge } from "./PortalStatusBadge";

export function ClientPortalCard({ workspaceId, client }: { workspaceId: string; client: Client }) {
  return (
    <section aria-labelledby="portal-title">
      <Card size="sm">
        <CardHeader>
          <CardTitle id="portal-title" className="flex items-center gap-3">
            Client portal
            <PortalStatusBadge status={client.portal.status} />
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <p className="text-sm text-muted-foreground">{description(client)}</p>
          <ClientPortalActions workspaceId={workspaceId} client={client} />
        </CardContent>
      </Card>
    </section>
  );
}

function description(client: Client): string {
  const expires = client.portal.expiresAtUtc ? formatDateTimeUtc(client.portal.expiresAtUtc) : null;

  switch (client.portal.status) {
    case "Active":
      return `${displayName(client)} has an account and can follow their projects in the portal. Their name, email, phone and company come from their own account, so only the notes are yours to edit.`;
    case "Invited":
      return `An invitation was sent to ${client.email}. It expires on ${expires}. Until then their details are locked, so the invitation keeps matching this address. Cancel the invitation to change them.`;
    case "Expired":
      return `The invitation to ${client.email} expired on ${expires}. Send a new one when you're ready.`;
    case "NotInvited":
      return client.archivedAtUtc
        ? "Restore this client to invite them to the portal."
        : "Invite this client by email so they can follow their projects in the portal. Once they accept, their name, email, phone and company come from their own account and can't be edited here.";
  }
}
