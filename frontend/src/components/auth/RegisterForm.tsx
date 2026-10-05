"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { postAuth } from "@/lib/auth/client";

type FormState = {
  message?: string;
  fieldErrors?: Record<string, string>;
  /** Kept so a failed attempt does not wipe what was typed; the password is deliberately not kept. */
  fullName?: string;
  email?: string;
};

export function RegisterForm() {
  const router = useRouter();

  const [state, submit, pending] = useActionState(
    async (_previous: FormState, formData: FormData): Promise<FormState> => {
      const fullName = String(formData.get("fullName") ?? "");
      const email = String(formData.get("email") ?? "");
      const result = await postAuth("register", {
        fullName,
        email,
        password: String(formData.get("password") ?? ""),
      });

      if (result.ok) {
        router.replace("/");
        router.refresh();
        return { fullName, email };
      }
      return { fullName, email, message: result.message, fieldErrors: result.fieldErrors };
    },
    {},
  );

  const fieldErrors = state.fieldErrors ?? {};
  // When every problem is shown next to its field, the summary line would only repeat them.
  const hasFieldErrors = Object.keys(fieldErrors).length > 0;

  return (
    <form action={submit} className="flex flex-col gap-6">
      <TextField
        label="Full name"
        name="fullName"
        autoComplete="name"
        required
        maxLength={200}
        defaultValue={state.fullName}
        error={fieldErrors.fullname}
      />
      <TextField
        label="Email"
        name="email"
        type="email"
        autoComplete="email"
        required
        maxLength={256}
        defaultValue={state.email}
        error={fieldErrors.email}
      />
      <TextField
        label="Password"
        name="password"
        type="password"
        autoComplete="new-password"
        required
        minLength={8}
        maxLength={128}
        hint="At least 8 characters."
        error={fieldErrors.password}
      />

      <FormError message={state.message && !hasFieldErrors ? state.message : undefined} />

      <Button
        type="submit"
        size="lg"
        className="w-full"
        disabled={pending}
      >
        {pending ? "Creating account…" : "Create account"}
      </Button>

      <p className="text-sm text-muted-foreground">
        Already have an account?{" "}
        <Link href="/login" className="font-medium text-foreground underline underline-offset-4">
          Sign in
        </Link>
      </p>
    </form>
  );
}
