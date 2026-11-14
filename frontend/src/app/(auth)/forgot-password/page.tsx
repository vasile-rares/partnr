import type { Metadata } from "next";
import { ForgotPasswordForm } from "@/components/auth/ForgotPasswordForm";

export const metadata: Metadata = { title: "Forgot password" };

export default function ForgotPasswordPage() {
  return (
    <>
      <h1 className="mb-3 font-heading text-3xl font-semibold">Forgot your password?</h1>
      <p className="mb-8 text-muted-foreground">Enter the email you sign in with and we will send you a link to choose a new one.</p>
      <ForgotPasswordForm />
    </>
  );
}
