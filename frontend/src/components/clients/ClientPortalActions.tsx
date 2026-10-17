"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import type { Client } from "@/lib/clients/types";

type State = { message?: string; done?: string };

/** Sends, resends and cancels the portal invitation; what is offered follows the status. */
export function ClientPortalActions({ workspaceId, client }: { workspaceId: string; client: Client }) {
  const router = useRouter();
  const url = `/api/workspaces/${workspaceId}/clients/${client.id}/invitation`;
  const status = client.portal.status;

  const [state, submit, pending] = useActionState(
    async (_previous: State, formData: FormData): Promise<State> => {
      const cancelling = formData.get("intent") === "cancel";
      const result = await sendJson<Client>(cancelling ? "DELETE" : "POST", url);

      if (result.ok) {
        router.refresh();
        return { done: cancelling ? "Invitation cancelled." : `Invitation sent to ${result.data.email}.` };
      }
      return { message: result.message };
    },
    {},
  );

  if (status === "Active" || client.archivedAtUtc !== null) {
    return null;
  }

  const sendLabel =
    status === "NotInvited" ? "Send invitation" : status === "Invited" ? "Resend invitation" : "Send a new invitation";

  return (
    <form action={submit} className="flex flex-col gap-3">
      <FormError message={state.message} />
      {state.done ? (
        <p role="status" className="text-sm text-status-done">
          {state.done}
        </p>
      ) : null}

      <div className="flex flex-wrap gap-3">
        <Button type="submit" name="intent" value="send" variant={status === "NotInvited" ? "default" : "outline"} disabled={pending}>
          {sendLabel}
        </Button>
        {status === "Invited" ? (
          <Button type="submit" name="intent" value="cancel" variant="ghost" disabled={pending}>
            Cancel invitation
          </Button>
        ) : null}
      </div>
    </form>
  );
}
