"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { TextField } from "@/components/auth/TextField";
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
    <form action={submit} className="flex flex-col gap-5">
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

      {state.message && !hasFieldErrors ? (
        <p role="alert" className="text-sm text-danger">
          {state.message}
        </p>
      ) : null}

      <button
        type="submit"
        disabled={pending}
        className="h-11 w-full rounded-md bg-done font-medium text-on-done outline-offset-2 focus-visible:outline-2 focus-visible:outline-done disabled:opacity-60"
      >
        {pending ? "Creating account…" : "Create account"}
      </button>

      <p className="text-sm text-ink-soft">
        Already have an account?{" "}
        <Link href="/login" className="font-medium text-done underline underline-offset-2">
          Sign in
        </Link>
      </p>
    </form>
  );
}
