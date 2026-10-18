"use client";

import type { FormEvent } from "react";
import { TextAreaField } from "@/components/forms/TextAreaField";
import { Button } from "@/components/ui/button";

type Props = {
  pending: boolean;
  onSubmit: (note: string) => void;
  onCancel: () => void;
};

/** Asks the client to sign off a deliverable that is already shared. The note is optional. */
export function ApprovalRequestForm({ pending, onSubmit, onCancel }: Props) {
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onSubmit(String(new FormData(event.currentTarget).get("note") ?? ""));
  }

  return (
    <form onSubmit={handleSubmit} className="mt-3 flex flex-col gap-4 rounded-lg border p-3">
      <TextAreaField
        label="Note for your client"
        name="note"
        maxLength={2000}
        rows={2}
        hint="Optional. Tell them what to look at."
      />
      <div className="flex gap-2">
        <Button type="submit" disabled={pending}>
          {pending ? "Sending…" : "Send request"}
        </Button>
        <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
