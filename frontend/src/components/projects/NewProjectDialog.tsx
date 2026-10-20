"use client";

import { PlusIcon } from "lucide-react";
import Link from "next/link";
import { ProjectForm } from "@/components/projects/ProjectForm";
import { Button, buttonVariants } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";

type NewProjectDialogProps = {
  workspaceId: string;
  /** Active clients only: an archived one cannot get a new project. */
  clients: { id: string; displayName: string }[];
  /** Preselected when the dialog opens from a client's page. */
  clientId?: string;
  variant?: "default" | "outline";
};

export function NewProjectDialog({ workspaceId, clients, clientId, variant = "default" }: NewProjectDialogProps) {
  return (
    <Dialog>
      <DialogTrigger render={<Button variant={variant} />}>
        <PlusIcon data-icon="inline-start" />
        New project
      </DialogTrigger>
      <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>New project</DialogTitle>
          <DialogDescription>
            A new project starts in Planning, so you can prepare it before your client sees it in their portal.
          </DialogDescription>
        </DialogHeader>
        {clients.length === 0 ? (
          <div className="flex flex-col items-start gap-4">
            <p className="text-muted-foreground">A project belongs to a client. Add your first client to start one.</p>
            <Link href={`/w/${workspaceId}/clients`} className={buttonVariants()}>
              Go to clients
            </Link>
          </div>
        ) : (
          <ProjectForm workspaceId={workspaceId} clients={clients} clientId={clientId} />
        )}
      </DialogContent>
    </Dialog>
  );
}
