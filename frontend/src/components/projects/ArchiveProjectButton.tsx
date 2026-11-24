"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { toast } from "sonner";
import { FormError } from "@/components/forms/FormMessage";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import type { Project } from "@/lib/projects/types";

type State = { message?: string };

export function ArchiveProjectButton({ workspaceId, project }: { workspaceId: string; project: Project }) {
  const router = useRouter();
  const archived = project.archivedAtUtc !== null;
  const action = archived ? "restore" : "archive";

  const [state, submit, pending] = useActionState(async (): Promise<State> => {
    const result = await sendJson<Project>("POST", `/api/workspaces/${workspaceId}/projects/${project.id}/${action}`);

    if (result.ok) {
      toast.success(archived ? "Project restored" : "Project archived");
      router.refresh();
      return {};
    }
    return { message: result.message };
  }, {});

  return (
    <form action={submit} className="archive-project-form flex flex-col gap-3">
      <FormError message={state.message} />
      <Button type="submit" variant={archived ? "default" : "outline"} disabled={pending}>
        {archived ? (pending ? "Restoring…" : "Restore project") : pending ? "Archiving…" : "Archive project"}
      </Button>
    </form>
  );
}
