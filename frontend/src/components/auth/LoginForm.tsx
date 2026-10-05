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
    <form action={submit} className="flex flex-col gap-6">
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

      <FormError message={state.message} />

      <Button
        type="submit"
        size="lg"
        className="w-full"
        disabled={pending}
      >
        {pending ? "Signing in…" : "Sign in"}
      </Button>

      <p className="text-sm text-muted-foreground">
        New here?{" "}
        <Link href="/register" className="font-medium text-foreground underline underline-offset-4">
          Create account
        </Link>
      </p>
    </form>
  );
}
