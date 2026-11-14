"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";

type FormState = { message?: string };

export function ResetPasswordForm({ token, email }: { token: string; email: string }) {
  const router = useRouter();

  const [state, submit, pending] = useActionState(async (_previous: FormState, formData: FormData): Promise<FormState> => {
    const result = await sendJson("POST", "/api/auth/reset-password", { token, password: String(formData.get("password") ?? "") });

    if (result.ok) {
      router.replace("/");
      router.refresh();
      return {};
    }
    return { message: result.fieldErrors.password ?? result.message };
  }, {});

  return (
    <form action={submit} className="reset-password-form flex flex-col gap-6">
      {/* Lets a password manager save the new password under the right account. */}
      <input type="email" name="username" autoComplete="username" value={email} readOnly hidden />
      <TextField
        label="New password"
        name="password"
        type="password"
        autoComplete="new-password"
        required
        minLength={8}
        maxLength={128}
        hint="At least 8 characters. Every other device you are signed in on will be signed out."
      />

      <FormError message={state.message} />

      <Button type="submit" size="lg" className="w-full" disabled={pending}>
        {pending ? "Saving…" : "Set the new password"}
      </Button>
    </form>
  );
}
