"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import type { Workspace } from "@/lib/workspaces/types";

type FormState = {
  /** Kept so a failed attempt does not wipe what was typed. */
  name?: string;
  message?: string;
  fieldError?: string;
};

export function CreateWorkspaceForm() {
  const router = useRouter();

  const [state, submit, pending] = useActionState(
    async (_previous: FormState, formData: FormData): Promise<FormState> => {
      const name = String(formData.get("name") ?? "");
      const result = await sendJson<Workspace>("POST", "/api/workspaces", { name });

      if (result.ok) {
        router.replace(`/w/${result.data.id}`);
        router.refresh();
        return { name };
      }
      return { name, message: result.message, fieldError: result.fieldErrors.name };
    },
    {},
  );

  return (
    <form action={submit} className="flex max-w-sm flex-col gap-6">
      <TextField
        label="Workspace name"
        name="name"
        required
        maxLength={100}
        autoComplete="organization"
        defaultValue={state.name}
        hint="Your own name or your business name. You can change it later."
        error={state.fieldError}
      />

      <FormError message={state.message && !state.fieldError ? state.message : undefined} />

      <Button
        type="submit"
        size="lg"
        className="w-full"
        disabled={pending}
      >
        {pending ? "Creating workspace…" : "Create workspace"}
      </Button>
    </form>
  );
}
