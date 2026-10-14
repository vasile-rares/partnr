import type { Metadata } from "next";
import { AppHeader } from "@/components/AppHeader";
import { Card } from "@/components/ui/card";
import { requireUser } from "@/lib/auth/session";
import { getPortalWorkspaces } from "@/lib/portal/server";

export const metadata: Metadata = { title: "Your portal" };

// What a client sees: the freelancers and agencies they work with. The projects, deliverables and approvals come in the
// next steps; for now this is where accepting an invitation lands, so the client knows it worked.
export default async function PortalPage() {
  await requireUser();
  const workspaces = await getPortalWorkspaces();

  return (
    <>
      <AppHeader />
      <main className="mx-auto w-full max-w-3xl px-6 py-10">
        <h1 className="font-heading text-3xl font-semibold">Your portal</h1>

        {workspaces.length === 0 ? (
          <p className="mt-6 max-w-prose text-muted-foreground">
            You are not connected to anyone yet. When a freelancer or agency invites you, they will show up here.
          </p>
        ) : (
          <>
            <p className="mt-3 max-w-prose text-muted-foreground">
              You work with the people below. Your projects, files and approvals will show up here.
            </p>
            <Card className="mt-8 gap-0 py-0">
              <ul className="divide-y">
                {workspaces.map((workspace) => (
                  <li key={workspace.id} className="px-4 py-3 font-medium">
                    {workspace.name}
                  </li>
                ))}
              </ul>
            </Card>
          </>
        )}
      </main>
    </>
  );
}
