import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Section } from "@/components/layout/Section";
import { Page } from "@/components/shell/Page";
import { InvitationList } from "@/components/team/InvitationList";
import { InviteMemberDialog } from "@/components/team/InviteMemberDialog";
import { MemberList } from "@/components/team/MemberList";
import { requireUser } from "@/lib/auth/session";
import { getMemberInvitations, getMembers } from "@/lib/team/server";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Team" };

export default async function TeamPage({ params }: PageProps<"/w/[workspaceId]/team">) {
  const user = await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }

  const isOwner = workspace.role === "Owner";
  // The invitations are the owner's: the API answers a member with 403, so only the owner's page asks.
  const [members, invitations] = await Promise.all([getMembers(workspace.id), isOwner ? getMemberInvitations(workspace.id) : Promise.resolve([])]);

  return (
    <Page crumbs={[{ label: "Team" }]} narrow>
      <div className="team-header mb-8 flex flex-wrap items-center justify-between gap-4">
        <h1 className="font-heading text-3xl font-semibold">Team</h1>
        {isOwner ? <InviteMemberDialog workspaceId={workspace.id} workspaceName={workspace.name} /> : null}
      </div>

      <div className="team-body flex flex-col gap-10">
        <Section title="Members" className="team-members">
          <MemberList workspaceId={workspace.id} members={members} currentUserId={user.id} canManage={isOwner} />
          {isOwner ? null : <p className="mt-3 text-sm text-muted-foreground">Only the owner of this workspace can invite or remove people.</p>}
        </Section>

        {isOwner ? (
          <Section title="Pending invitations" className="team-invitations">
            <InvitationList workspaceId={workspace.id} invitations={invitations} />
          </Section>
        ) : null}
      </div>
    </Page>
  );
}
