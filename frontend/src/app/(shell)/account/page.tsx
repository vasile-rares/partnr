import type { Metadata } from "next";
import { GoogleCalendarCard } from "@/components/account/GoogleCalendarCard";
import { GoogleResultMessage } from "@/components/account/GoogleResultMessage";
import { ProfileForm } from "@/components/account/ProfileForm";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getGoogleConnection } from "@/lib/google/server";
import { parseGoogleResult } from "@/lib/google/types";
import { getMyWorkspaces } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Your profile" };

// The profile belongs to the account, not to a workspace, so it lives outside /w/ and works with no workspace at all.
export default async function AccountPage({ searchParams }: PageProps<"/account">) {
  const user = await requireUser();
  // Google Calendar is for the people who schedule meetings, not for someone who is only a client.
  const worksInAWorkspace = (await getMyWorkspaces()).length > 0;
  const googleResult = parseGoogleResult((await searchParams).google);

  return (
    <Page crumbs={[{ label: "Account" }]} narrow>
      <h1 className="font-heading text-3xl font-semibold">Your profile</h1>
      <p className="mt-3 mb-8 max-w-prose text-muted-foreground">
        The people you work with see these details, including the freelancers whose client you are.
      </p>

      {worksInAWorkspace && googleResult ? <GoogleResultMessage result={googleResult} /> : null}

      <dl className="account-email mb-8 max-w-lg">
        <dt className="text-sm font-medium">Email</dt>
        <dd className="mt-1">{user.email}</dd>
        <dd className="mt-1 text-sm text-muted-foreground">You can&apos;t change your email yet.</dd>
      </dl>

      <ProfileForm fullName={user.fullName} phone={user.phone} company={user.company} />

      {worksInAWorkspace ? <GoogleCalendarCard connection={await getGoogleConnection()} /> : null}
    </Page>
  );
}
