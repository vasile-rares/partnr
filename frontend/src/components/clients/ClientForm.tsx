"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { TextAreaField } from "@/components/forms/TextAreaField";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import type { Client } from "@/lib/clients/types";

type Values = { name: string; email: string; company: string; phone: string; notes: string };

type FormState = {
  values: Values;
  saved?: boolean;
  message?: string;
  fieldErrors?: Record<string, string>;
  emailTaken?: boolean;
};

const valuesOf = (client: Client): Values => ({
  name: client.name ?? "",
  email: client.email,
  company: client.company ?? "",
  phone: client.phone ?? "",
  notes: client.notes ?? "",
});

/** Edits a client. A new one is added with `AddClientForm`. */
export function ClientForm({ workspaceId, client }: { workspaceId: string; client: Client }) {
  const router = useRouter();

  const [state, submit, pending] = useActionState(
    async (_previous: FormState, formData: FormData): Promise<FormState> => {
      const text = (key: keyof Values) => String(formData.get(key) ?? "");
      const values: Values = {
        name: text("name"),
        email: text("email"),
        company: text("company"),
        phone: text("phone"),
        notes: text("notes"),
      };

      const result = await sendJson<Client>("PUT", `/api/workspaces/${workspaceId}/clients/${client.id}`, values);

      if (result.ok) {
        router.refresh();
        return { values: valuesOf(result.data), saved: true };
      }
      return { values, message: result.message, fieldErrors: result.fieldErrors, emailTaken: result.status === 409 };
    },
    { values: valuesOf(client) },
  );

  const errors = state.fieldErrors ?? {};
  const emailError = errors.email ?? (state.emailTaken ? state.message : undefined);
  const hasFieldError = Object.keys(errors).length > 0 || state.emailTaken;
  // Locked while an invitation waits or the client has an account: the inputs stay read-only in the form (screen readers
  // still read them, their values go back unchanged), only the notes can change.
  const locked = client.detailsLocked;

  return (
    <form action={submit} className="flex max-w-lg flex-col gap-6">
      <TextField
        label="Name"
        name="name"
        maxLength={200}
        readOnly={locked}
        defaultValue={state.values.name}
        hint={locked ? undefined : "Optional. Until they have an account, the list shows their email."}
        error={errors.name}
      />
      <TextField
        label="Email"
        name="email"
        type="email"
        required
        maxLength={320}
        autoComplete="off"
        readOnly={locked}
        defaultValue={state.values.email}
        error={emailError}
      />
      <TextField
        label="Company"
        name="company"
        maxLength={200}
        readOnly={locked}
        defaultValue={state.values.company}
        error={errors.company}
      />
      <TextField
        label="Phone"
        name="phone"
        type="tel"
        maxLength={50}
        readOnly={locked}
        defaultValue={state.values.phone}
        error={errors.phone}
      />
      <TextAreaField
        label="Notes"
        name="notes"
        maxLength={2000}
        rows={4}
        defaultValue={state.values.notes}
        hint="Only you and your team see this."
        error={errors.notes}
      />

      <FormError message={hasFieldError ? undefined : state.message} />
      {state.saved ? (
        <p role="status" className="text-sm text-status-done">
          Changes saved.
        </p>
      ) : null}

      <Button type="submit" size="lg" className="w-full" disabled={pending}>
        {pending ? "Saving…" : locked ? "Save notes" : "Save changes"}
      </Button>
    </form>
  );
}
