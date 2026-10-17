"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";

type FormState = {
  fullName?: string;
  phone?: string;
  company?: string;
  message?: string;
  fieldErrors?: Record<string, string>;
};

/** The invited email is only shown, never sent: the API takes it from the invitation. */
export function InvitationRegisterForm({ token, email }: { token: string; email: string }) {
  const router = useRouter();

  const [state, submit, pending] = useActionState(
    async (_previous: FormState, formData: FormData): Promise<FormState> => {
      const text = (key: string) => String(formData.get(key) ?? "");
      const values = { fullName: text("fullName"), phone: text("phone"), company: text("company") };

      const result = await sendJson("POST", `/api/invitations/${token}/register`, {
        ...values,
        password: text("password"),
      });

      if (result.ok) {
        router.replace("/portal");
        router.refresh();
        return values;
      }
      return { ...values, message: result.message, fieldErrors: result.fieldErrors };
    },
    {},
  );

  const errors = state.fieldErrors ?? {};
  const hasFieldErrors = Object.keys(errors).length > 0;

  return (
    <form action={submit} className="flex flex-col gap-6">
      <TextField
        label="Email"
        type="email"
        readOnly
        defaultValue={email}
        hint="The address this invitation was sent to. It can't be changed."
      />
      <TextField
        label="Full name"
        name="fullName"
        autoComplete="name"
        required
        maxLength={200}
        defaultValue={state.fullName}
        error={errors.fullname}
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
        error={errors.password}
      />
      <TextField
        label="Phone (optional)"
        name="phone"
        type="tel"
        autoComplete="tel"
        maxLength={50}
        defaultValue={state.phone}
        error={errors.phone}
      />
      <TextField
        label="Company (optional)"
        name="company"
        autoComplete="organization"
        maxLength={200}
        defaultValue={state.company}
        error={errors.company}
      />

      <FormError message={state.message && !hasFieldErrors ? state.message : undefined} />

      <Button type="submit" size="lg" className="w-full" disabled={pending}>
        {pending ? "Creating account…" : "Create account and accept"}
      </Button>
    </form>
  );
}
