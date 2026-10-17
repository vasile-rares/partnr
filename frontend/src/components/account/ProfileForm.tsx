"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import type { CurrentUser } from "@/lib/auth/session";

type Values = { fullName: string; phone: string; company: string };

type FormState = {
  values: Values;
  saved?: boolean;
  message?: string;
  fieldErrors?: Record<string, string>;
};

type ProfileFormProps = { fullName: string; phone: string | null; company: string | null };

export function ProfileForm({ fullName, phone, company }: ProfileFormProps) {
  const router = useRouter();

  const [state, submit, pending] = useActionState(
    async (_previous: FormState, formData: FormData): Promise<FormState> => {
      const text = (key: keyof Values) => String(formData.get(key) ?? "");
      const values: Values = { fullName: text("fullName"), phone: text("phone"), company: text("company") };

      const result = await sendJson<CurrentUser>("PUT", "/api/auth/me", values);

      if (result.ok) {
        router.refresh();
        const saved = result.data;
        return { values: { fullName: saved.fullName, phone: saved.phone ?? "", company: saved.company ?? "" }, saved: true };
      }
      return { values, message: result.message, fieldErrors: result.fieldErrors };
    },
    { values: { fullName, phone: phone ?? "", company: company ?? "" } },
  );

  const errors = state.fieldErrors ?? {};
  const formError = Object.keys(errors).length > 0 ? undefined : state.message;

  return (
    <form action={submit} className="flex max-w-lg flex-col gap-6">
      <TextField
        label="Full name"
        name="fullName"
        required
        maxLength={200}
        autoComplete="name"
        defaultValue={state.values.fullName}
        error={errors.fullname}
      />
      <TextField
        label="Phone"
        name="phone"
        type="tel"
        maxLength={50}
        autoComplete="tel"
        defaultValue={state.values.phone}
        error={errors.phone}
      />
      <TextField
        label="Company"
        name="company"
        maxLength={200}
        autoComplete="organization"
        defaultValue={state.values.company}
        error={errors.company}
      />

      <FormError message={formError} />
      {state.saved ? (
        <p role="status" className="text-sm text-status-done">
          Profile saved.
        </p>
      ) : null}

      <Button type="submit" size="lg" className="w-full" disabled={pending}>
        {pending ? "Saving…" : "Save profile"}
      </Button>
    </form>
  );
}
