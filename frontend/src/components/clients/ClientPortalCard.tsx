import { ArchiveIcon, CircleCheckIcon, ClockAlertIcon, MailIcon, UserRoundPlusIcon, type LucideIcon } from "lucide-react";
import { InfoTip } from "@/components/actions/InfoTip";
import { IconTile } from "@/components/reui/icon-tile";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { displayName, type Client } from "@/lib/clients/types";
import { formatDate, formatDateTimeUtc } from "@/lib/format";
import { ClientPortalActions } from "./ClientPortalActions";

export function ClientPortalCard({ workspaceId, client }: { workspaceId: string; client: Client }) {
  return (
    <section aria-labelledby="portal-title" className="client-portal-card">
      <Card size="sm">
        <CardHeader>
          <CardTitle id="portal-title" className="flex items-center gap-1">
            Status
            {client.archivedAtUtc ? null : <InfoTip label="About the client portal">{explanation(client)}</InfoTip>}
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <PortalState client={client} />
          <ClientPortalActions workspaceId={workspaceId} client={client} />
        </CardContent>
      </Card>
    </section>
  );
}

function PortalState({ client }: { client: Client }) {
  const { icon: Icon, tile, title, detail } = state(client);

  return (
    <div className="client-portal-state flex items-center gap-3">
      <IconTile aria-hidden variant={tile.variant} className={tile.className}>
        <Icon />
      </IconTile>
      <div className="min-w-0">
        <p className="font-medium">{title}</p>
        <p className="text-sm text-muted-foreground">{detail}</p>
      </div>
    </div>
  );
}

type Tile = { variant: "soft" | "outline"; className: string };

function state(client: Client): { icon: LucideIcon; tile: Tile; title: string; detail: string } {
  const expires = client.portal.expiresAtUtc ? formatDate(client.portal.expiresAtUtc.slice(0, 10)) : "";

  switch (client.portal.status) {
    case "Active":
      return { icon: CircleCheckIcon, tile: { variant: "soft", className: "text-status-done" }, title: "Active", detail: "Has an account" };
    case "Invited":
      return { icon: MailIcon, tile: { variant: "soft", className: "text-status-doing" }, title: "Invited", detail: `Expires ${expires}` };
    case "Expired":
      return { icon: ClockAlertIcon, tile: { variant: "soft", className: "text-destructive" }, title: "Invitation expired", detail: `Expired ${expires}` };
    case "NotInvited":
      return client.archivedAtUtc
        ? { icon: ArchiveIcon, tile: { variant: "outline", className: "text-muted-foreground" }, title: "Archived", detail: "Restore to invite them" }
        : { icon: UserRoundPlusIcon, tile: { variant: "outline", className: "text-muted-foreground" }, title: "Not invited yet", detail: "No access to the portal" };
  }
}

function explanation(client: Client): string {
  const expires = client.portal.expiresAtUtc ? formatDateTimeUtc(client.portal.expiresAtUtc) : null;

  switch (client.portal.status) {
    case "Active":
      return `${displayName(client)} has an account and can follow their projects in the portal. Their name, email, phone and company come from their own account, so only the notes are yours to edit.`;
    case "Invited":
      return `An invitation was sent to ${client.email}. It expires on ${expires}. Until then their details are locked, so the invitation keeps matching this address. Cancel the invitation to change them.`;
    case "Expired":
      return `The invitation to ${client.email} expired on ${expires}. Send a new one when you're ready.`;
    case "NotInvited":
      return "Invite this client by email so they can follow their projects in the portal. Once they accept, their name, email, phone and company come from their own account and can't be edited here.";
  }
}
