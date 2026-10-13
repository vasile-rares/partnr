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
  /** Kept so a failed attempt does not wipe what was typed. */
  values: Values;
  saved?: boolean;
  message?: string;
  fieldErrors?: Record<string, string>;
  /** The API rejected the email as already used (409), so the message belongs under that field. */
  emailTaken?: boolean;
};

const empty: Values = { name: "", email: "", company: "", phone: "", notes: "" };

const valuesOf = (client: Client): Values => ({
  name: client.name,
  email: client.email,
  company: client.company ?? "",
  phone: client.phone ?? "",
  notes: client.notes ?? "",
});

/** Adds a client, or edits `client` when given. */
export function ClientForm({ workspaceId, client }: { workspaceId: string; client?: Client }) {
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

      const result = client
        ? await sendJson<Client>("PUT", `/api/workspaces/${workspaceId}/clients/${client.id}`, values)
        : await sendJson<Client>("POST", `/api/workspaces/${workspaceId}/clients`, values);

      if (result.ok) {
        if (client) {
          // Refresh so the title and everything else showing the client pick up the change.
          router.refresh();
          return { values: valuesOf(result.data), saved: true };
        }
        router.replace(`/w/${workspaceId}/clients`);
        router.refresh();
        return { values };
      }
      return { values, message: result.message, fieldErrors: result.fieldErrors, emailTaken: result.status === 409 };
    },
    { values: client ? valuesOf(client) : empty },
  );

  const errors = state.fieldErrors ?? {};
  const emailError = errors.email ?? (state.emailTaken ? state.message : undefined);
  const hasFieldError = Object.keys(errors).length > 0 || state.emailTaken;

  return (
    <form action={submit} className="flex max-w-lg flex-col gap-6">
      <TextField
        label="Name"
        name="name"
        required
        maxLength={200}
        defaultValue={state.values.name}
        error={errors.name}
      />
      <TextField
        label="Email"
        name="email"
        type="email"
        required
        maxLength={320}
        autoComplete="off"
        defaultValue={state.values.email}
        error={emailError}
      />
      <TextField
        label="Company"
        name="company"
        maxLength={200}
        defaultValue={state.values.company}
        error={errors.company}
      />
      <TextField
        label="Phone"
        name="phone"
        type="tel"
        maxLength={50}
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
        {client ? (pending ? "Saving…" : "Save changes") : pending ? "Adding client…" : "Add client"}
      </Button>
    </form>
  );
}
