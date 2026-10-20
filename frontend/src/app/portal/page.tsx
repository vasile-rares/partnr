import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/shell/AppShell";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getPortalWorkspaces } from "@/lib/portal/server";

export const metadata: Metadata = { title: "Your portal" };

// The portal of one freelancer or agency lives at /portal/{id}; this is only the way in. Several firms: the first one,
// and the sidebar's switcher lists the others.
export default async function PortalPage() {
  await requireUser();
  const [first] = await getPortalWorkspaces();
  if (first) {
    redirect(`/portal/${first.id}`);
  }

  return (
    <AppShell context={{ kind: "none" }}>
      <Page crumbs={[{ label: "Portal" }]} narrow>
        <h1 className="font-heading text-3xl font-semibold">Your portal</h1>
        <p className="mt-6 max-w-prose text-muted-foreground">
          You are not connected to anyone yet. When a freelancer or agency invites you, they will show up here.
        </p>
      </Page>
    </AppShell>
  );
}
