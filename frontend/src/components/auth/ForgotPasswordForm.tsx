"use client";

import Link from "next/link";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";

type FormState = { sentTo?: string; email?: string; message?: string };

export function ForgotPasswordForm() {
  const [state, submit, pending] = useActionState(async (_previous: FormState, formData: FormData): Promise<FormState> => {
    const email = String(formData.get("email") ?? "").trim();
    const result = await sendJson("POST", "/api/auth/forgot-password", { email });

    if (result.ok) {
      return { sentTo: email };
    }
    if (result.status === 429) {
      return { email, message: "Too many requests right now. Try again in a minute." };
    }
    return { email, message: result.fieldErrors.email ?? result.message };
  }, {});

  // The same words whether or not the address has an account: the page must not tell which emails exist.
  if (state.sentTo) {
    return (
      <div className="forgot-password-sent flex flex-col gap-4 text-muted-foreground" role="status">
        <p>
          If an account exists for <span className="font-medium text-foreground">{state.sentTo}</span>, we sent it a link to
          choose a new password. The link works for one hour.
        </p>
        <p className="text-sm">Nothing arrived? Check the spam folder, or ask again in a minute.</p>
        <Link href="/login" className="text-sm font-medium text-foreground underline underline-offset-4">
          Back to sign in
        </Link>
      </div>
    );
  }

  return (
    <form action={submit} className="forgot-password-form flex flex-col gap-6">
      <TextField label="Email" name="email" type="email" autoComplete="email" required maxLength={256} defaultValue={state.email} />

      <FormError message={state.message} />

      <Button type="submit" size="lg" className="w-full" disabled={pending}>
        {pending ? "Sending…" : "Send the link"}
      </Button>

      <p className="text-sm text-muted-foreground">
        Remembered it?{" "}
        <Link href="/login" className="font-medium text-foreground underline underline-offset-4">
          Sign in
        </Link>
      </p>
    </form>
  );
}
