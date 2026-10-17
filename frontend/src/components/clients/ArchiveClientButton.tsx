"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import type { Client } from "@/lib/clients/types";

type State = { message?: string };

export function ArchiveClientButton({ workspaceId, client }: { workspaceId: string; client: Client }) {
  const router = useRouter();
  const archived = client.archivedAtUtc !== null;
  const action = archived ? "restore" : "archive";

  const [state, submit, pending] = useActionState(async (): Promise<State> => {
    const result = await sendJson<Client>("POST", `/api/workspaces/${workspaceId}/clients/${client.id}/${action}`);

    if (result.ok) {
      router.refresh();
      return {};
    }
    return { message: result.message };
  }, {});

  return (
    <form action={submit} className="flex flex-col gap-3">
      <FormError message={state.message} />
      <Button type="submit" variant={archived ? "default" : "outline"} disabled={pending}>
        {archived ? (pending ? "Restoring…" : "Restore client") : pending ? "Archiving…" : "Archive client"}
      </Button>
    </form>
  );
}
