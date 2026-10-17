"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import type { Workspace } from "@/lib/workspaces/types";

type FormState = {
  name: string;
  saved?: boolean;
  message?: string;
  fieldError?: string;
};

export function RenameWorkspaceForm({ workspaceId, name }: { workspaceId: string; name: string }) {
  const router = useRouter();

  const [state, submit, pending] = useActionState(
    async (_previous: FormState, formData: FormData): Promise<FormState> => {
      const newName = String(formData.get("name") ?? "");
      const result = await sendJson<Workspace>("PATCH", `/api/workspaces/${workspaceId}`, { name: newName });

      if (result.ok) {
        router.refresh();
        return { name: result.data.name, saved: true };
      }
      return { name: newName, message: result.message, fieldError: result.fieldErrors.name };
    },
    { name },
  );

  return (
    <form action={submit} className="flex max-w-sm flex-col gap-6">
      <TextField
        label="Workspace name"
        name="name"
        required
        maxLength={100}
        defaultValue={state.name}
        error={state.fieldError}
      />

      <FormError message={state.message && !state.fieldError ? state.message : undefined} />
      {state.saved ? (
        <p role="status" className="text-sm text-status-done">
          Name saved.
        </p>
      ) : null}

      <Button
        type="submit"
        size="lg"
        className="w-full"
        disabled={pending}
      >
        {pending ? "Saving…" : "Save name"}
      </Button>
    </form>
  );
}
