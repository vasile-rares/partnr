"use client";

import { PlusIcon } from "lucide-react";
import { useState } from "react";
import { AddClientForm } from "@/components/clients/AddClientForm";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";

type AddClientDialogProps = {
  workspaceId: string;
  /** The emails of every client of the workspace, archived included, to warn about a duplicate. */
  existingEmails: string[];
};

export function AddClientDialog({ workspaceId, existingEmails }: AddClientDialogProps) {
  const [open, setOpen] = useState(false);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={<Button />}>
        <PlusIcon data-icon="inline-start" />
        Add client
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Add a client</DialogTitle>
          <DialogDescription>
            Enter their email. They get an invitation to follow their projects, and their name, phone and company come from
            their own account once they accept.
          </DialogDescription>
        </DialogHeader>
        <AddClientForm workspaceId={workspaceId} existingEmails={existingEmails} onDone={() => setOpen(false)} />
      </DialogContent>
    </Dialog>
  );
}
