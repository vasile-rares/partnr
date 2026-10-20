import type { Metadata } from "next";
import { ProfileForm } from "@/components/account/ProfileForm";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";

export const metadata: Metadata = { title: "Your profile" };

// The profile belongs to the account, not to a workspace, so it lives outside /w/ and works with no workspace at all.
export default async function AccountPage() {
  const user = await requireUser();

  return (
    <Page crumbs={[{ label: "Account" }]} narrow>
      <h1 className="font-heading text-3xl font-semibold">Your profile</h1>
      <p className="mt-3 mb-8 max-w-prose text-muted-foreground">
        The people you work with see these details, including the freelancers whose client you are.
      </p>

      <dl className="mb-8 max-w-lg">
        <dt className="text-sm font-medium">Email</dt>
        <dd className="mt-1">{user.email}</dd>
        <dd className="mt-1 text-sm text-muted-foreground">You can&apos;t change your email yet.</dd>
      </dl>

      <ProfileForm fullName={user.fullName} phone={user.phone} company={user.company} />
    </Page>
  );
}
