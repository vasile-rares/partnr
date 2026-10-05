"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { TextField } from "@/components/auth/TextField";
import { postAuth } from "@/lib/auth/client";

type FormState = {
  message?: string;
  /** Kept so a failed attempt does not wipe the email; the password is deliberately not kept. */
  email?: string;
};

export function LoginForm({ next }: { next: string }) {
  const router = useRouter();

  const [state, submit, pending] = useActionState(
    async (_previous: FormState, formData: FormData): Promise<FormState> => {
      const email = String(formData.get("email") ?? "");
      const result = await postAuth("login", { email, password: String(formData.get("password") ?? "") });

      if (result.ok) {
        router.replace(next);
        router.refresh();
        return { email };
      }
      return { email, message: result.message };
    },
    {},
  );

  return (
    <form action={submit} className="flex flex-col gap-5">
      <TextField
        label="Email"
        name="email"
        type="email"
        autoComplete="email"
        required
        maxLength={256}
        defaultValue={state.email}
      />
      <TextField
        label="Password"
        name="password"
        type="password"
        autoComplete="current-password"
        required
        maxLength={128}
      />

      {state.message ? (
        <p role="alert" className="text-sm text-danger">
          {state.message}
        </p>
      ) : null}

      <button
        type="submit"
        disabled={pending}
        className="h-11 w-full rounded-md bg-done font-medium text-on-done outline-offset-2 focus-visible:outline-2 focus-visible:outline-done disabled:opacity-60"
      >
        {pending ? "Signing in…" : "Sign in"}
      </button>

      <p className="text-sm text-ink-soft">
        New here?{" "}
        <Link href="/register" className="font-medium text-done underline underline-offset-2">
          Create account
        </Link>
      </p>
    </form>
  );
}
