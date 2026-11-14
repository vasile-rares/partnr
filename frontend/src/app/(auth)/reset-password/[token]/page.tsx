import type { Metadata } from "next";
import Link from "next/link";
import type { ReactNode } from "react";
import { ResetPasswordForm } from "@/components/auth/ResetPasswordForm";
import { buttonVariants } from "@/components/ui/button";
import { getPasswordResetLink } from "@/lib/passwordReset/server";

export const metadata: Metadata = { title: "Choose a new password" };

// Public on purpose (see proxy.ts): whoever forgot their password is signed out. The token proves they got the email.
export default async function ResetPasswordPage({ params }: PageProps<"/reset-password/[token]">) {
  const { token } = await params;
  const link = await getPasswordResetLink(token);

  if (link.status === "invalid") {
    return (
      <Message title="Link not valid">
        This link isn&apos;t valid. Check that you copied all of it from the email, or ask for a new one.
      </Message>
    );
  }
  if (link.status === "unusable") {
    return <Message title="Link can't be used">{link.message}</Message>;
  }

  return (
    <>
      <h1 className="mb-3 font-heading text-3xl font-semibold">Choose a new password</h1>
      <p className="mb-8 text-muted-foreground">
        For <span className="font-medium text-foreground">{link.email}</span>.
      </p>
      <ResetPasswordForm token={token} email={link.email} />
    </>
  );
}

function Message({ title, children }: { title: string; children: ReactNode }) {
  return (
    <>
      <h1 className="mb-3 font-heading text-3xl font-semibold">{title}</h1>
      <div className="reset-password-message flex flex-col text-muted-foreground">
        <p>{children}</p>
        <Link href="/forgot-password" className={buttonVariants({ size: "lg", className: "mt-6 w-full" })}>
          Ask for a new link
        </Link>
      </div>
    </>
  );
}
