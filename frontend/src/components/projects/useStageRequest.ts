"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { sendJson } from "@/lib/api/client";
import type { Project } from "@/lib/projects/types";

/** Every change to a stage is one request that answers with the whole project, so the page just refreshes. */
export function useStageRequest(workspaceId: string, projectId: string) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState<string>();
  const base = `/api/workspaces/${workspaceId}/projects/${projectId}/stages`;

  function run(method: "POST" | "PUT" | "DELETE", path: string, body: unknown, onDone?: () => void, onFail?: () => void) {
    startTransition(async () => {
      setMessage(undefined);
      const result = await sendJson<Project>(method, `${base}${path}`, body);
      if (result.ok) {
        onDone?.();
        router.refresh();
      } else {
        setMessage(result.message);
        onFail?.();
      }
    });
  }

  return { pending, message, setMessage, run };
}
