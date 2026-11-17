"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";

type State = { message?: string };

/** For someone already signed in with the invited address: one click connects them to the workspace. */
export function AcceptInvitationButton({ token }: { token: string }) {
  const router = useRouter();

  const [state, submit, pending] = useActionState(async (): Promise<State> => {
    const result = await sendJson<{ workspaceId: string }>("POST", `/api/invitations/${token}/accept`);

    if (result.ok) {
      router.replace(`/w/${result.data.workspaceId}`);
      router.refresh();
      return {};
    }
    return { message: result.message };
  }, {});

  return (
    <form action={submit} className="accept-invitation-form flex flex-col gap-6">
      <FormError message={state.message} />
      <Button type="submit" size="lg" className="w-full" disabled={pending}>
        {pending ? "Accepting…" : "Accept invitation"}
      </Button>
    </form>
  );
}
