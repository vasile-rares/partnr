"use client";

import { UserPlusIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useActionState, useState } from "react";
import { toast } from "sonner";
import { FormError } from "@/components/forms/FormMessage";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { sendJson } from "@/lib/api/client";
import type { MemberInvitation } from "@/lib/team/types";

type FormState = { email?: string; message?: string; fieldError?: string };

/** Owner only. Inviting an address that already has an open invitation replaces it, so this is also how one is resent. */
export function InviteMemberDialog({ workspaceId, workspaceName }: { workspaceId: string; workspaceName: string }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);

  const [state, submit, pending] = useActionState(
    async (_previous: FormState, formData: FormData): Promise<FormState> => {
      const email = String(formData.get("email") ?? "");
      const result = await sendJson<MemberInvitation>("POST", `/api/workspaces/${workspaceId}/member-invitations`, { email });

      if (result.ok) {
        toast.success(`Invitation sent to ${result.data.email}`);
        setOpen(false);
        router.refresh();
        return {};
      }
      return { email, message: result.message, fieldError: result.fieldErrors.email };
    },
    {},
  );

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={<Button />}>
        <UserPlusIcon data-icon="inline-start" />
        Invite member
      </DialogTrigger>
      <DialogContent className="invite-member-dialog sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Invite a team member</DialogTitle>
          <DialogDescription>
            Enter their email. They get an invitation to join {workspaceName} and, once they accept, can see all its clients and
            projects.
          </DialogDescription>
        </DialogHeader>
        <form action={submit} className="invite-member-form flex flex-col gap-6">
          <TextField
            label="Email"
            name="email"
            type="email"
            required
            maxLength={320}
            autoComplete="off"
            placeholder="name@company.com"
            defaultValue={state.email}
            error={state.fieldError}
          />
          <FormError message={state.fieldError ? undefined : state.message} />
          <Button type="submit" size="lg" className="w-full" disabled={pending}>
            {pending ? "Sending…" : "Send invitation"}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  );
}
