"use client";

import { ArrowDownIcon, ArrowUpIcon, PencilIcon, Trash2Icon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useRef, useState, useTransition, type FormEvent } from "react";
import { IconButton } from "@/components/actions/IconButton";
import { FormError } from "@/components/forms/FormMessage";
import { SelectField } from "@/components/forms/SelectField";
import { TextAreaField } from "@/components/forms/TextAreaField";
import { TextField } from "@/components/forms/TextField";
import { ProjectProgress } from "@/components/projects/ProjectProgress";
import { StageStatusBadge } from "@/components/projects/StageStatusBadge";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import { formatDate } from "@/lib/format";
import { stageStatuses, type Project, type Stage } from "@/lib/projects/types";

/** Each change is one request that answers with the whole project, so the page just refreshes. Read-only when archived. */
export function StageList({ workspaceId, project }: { workspaceId: string; project: Project }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [editing, setEditing] = useState<string | null>(null);
  const [message, setMessage] = useState<string>();
  const addForm = useRef<HTMLFormElement>(null);
  const readOnly = project.archivedAtUtc !== null;
  const base = `/api/workspaces/${workspaceId}/projects/${project.id}/stages`;

  function run(method: "POST" | "PUT" | "DELETE", path: string, body: unknown, onDone?: () => void) {
    startTransition(async () => {
      setMessage(undefined);
      const result = await sendJson<Project>(method, `${base}${path}`, body);
      if (result.ok) {
        onDone?.();
        router.refresh();
      } else {
        setMessage(result.message);
      }
    });
  }

  function onAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    run(
      "POST",
      "",
      {
        name: String(data.get("name") ?? ""),
        description: String(data.get("description") ?? ""),
        dueDate: String(data.get("dueDate") ?? "") || null,
      },
      () => addForm.current?.reset(),
    );
  }

  return (
    <section aria-labelledby="stages-title" className="mb-12 max-w-lg">
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h2 id="stages-title" className="font-heading text-xl font-semibold">
          Stages
        </h2>
        <ProjectProgress progress={project.progress} />
      </div>

      {project.stages.length === 0 ? (
        <p className="mb-6 text-sm text-muted-foreground">
          {readOnly ? "This project has no stages." : "No stages yet. Break the project into steps your client can follow."}
        </p>
      ) : (
        <ol className="mb-6 flex flex-col divide-y rounded-lg border">
          {project.stages.map((stage, index) => (
            <li key={stage.id} className="p-3">
              {editing === stage.id ? (
                <StageEditForm
                  stage={stage}
                  pending={pending}
                  onCancel={() => setEditing(null)}
                  onSave={(values) => run("PUT", `/${stage.id}`, values, () => setEditing(null))}
                />
              ) : (
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">{stage.name}</p>
                    {stage.description ? <p className="mt-1 text-sm text-muted-foreground">{stage.description}</p> : null}
                    <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                      <StageStatusBadge status={stage.status} />
                      {stage.dueDate ? <span>Due {formatDate(stage.dueDate)}</span> : null}
                    </div>
                  </div>
                  {readOnly ? null : (
                    <div className="flex shrink-0 gap-1">
                      <IconButton
                        icon={ArrowUpIcon}
                        tooltip="Move up"
                        disabled={pending || index === 0}
                        aria-label={`Move ${stage.name} up`}
                        onClick={() => run("POST", `/${stage.id}/move`, { direction: "up" })}
                      />
                      <IconButton
                        icon={ArrowDownIcon}
                        tooltip="Move down"
                        disabled={pending || index === project.stages.length - 1}
                        aria-label={`Move ${stage.name} down`}
                        onClick={() => run("POST", `/${stage.id}/move`, { direction: "down" })}
                      />
                      <IconButton
                        icon={PencilIcon}
                        tooltip="Edit"
                        disabled={pending}
                        aria-label={`Edit ${stage.name}`}
                        onClick={() => setEditing(stage.id)}
                      />
                      <DeleteStageButton stage={stage} disabled={pending} onConfirm={() => run("DELETE", `/${stage.id}`, undefined)} />
                    </div>
                  )}
                </div>
              )}
            </li>
          ))}
        </ol>
      )}

      <FormError message={message} />

      {readOnly ? null : (
        <form ref={addForm} onSubmit={onAdd} className="mt-4 flex flex-col gap-4 rounded-lg border p-4">
          <h3 className="font-medium">Add a stage</h3>
          <TextField label="Stage name" name="name" required maxLength={200} />
          <TextAreaField label="Stage description" name="description" maxLength={2000} rows={2} hint="Optional." />
          <TextField label="Stage due date" name="dueDate" type="date" />
          <Button type="submit" disabled={pending}>
            {pending ? "Saving…" : "Add stage"}
          </Button>
        </form>
      )}
    </section>
  );
}

type StageValues = { name: string; description: string; dueDate: string | null; status: string };

function StageEditForm({
  stage,
  pending,
  onSave,
  onCancel,
}: {
  stage: Stage;
  pending: boolean;
  onSave: (values: StageValues) => void;
  onCancel: () => void;
}) {
  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    onSave({
      name: String(data.get("name") ?? ""),
      description: String(data.get("description") ?? ""),
      dueDate: String(data.get("dueDate") ?? "") || null,
      status: String(data.get("status") ?? ""),
    });
  }

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-4">
      <TextField label="Name" name="name" required maxLength={200} defaultValue={stage.name} />
      <TextAreaField label="Description" name="description" maxLength={2000} rows={2} defaultValue={stage.description ?? ""} />
      <TextField label="Due date" name="dueDate" type="date" defaultValue={stage.dueDate ?? ""} />
      <SelectField label="Status" name="status" required defaultValue={stage.status} options={stageStatuses} />
      <div className="flex gap-2">
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : "Save stage"}
        </Button>
        <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

// Removing a stage cannot be undone, so it asks first.
function DeleteStageButton({ stage, disabled, onConfirm }: { stage: Stage; disabled: boolean; onConfirm: () => void }) {
  return (
    <AlertDialog>
      <AlertDialogTrigger
        render={<IconButton icon={Trash2Icon} tooltip="Delete" disabled={disabled} aria-label={`Delete ${stage.name}`} />}
      />
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete this stage?</AlertDialogTitle>
          <AlertDialogDescription>
            “{stage.name}” is removed from the project and from your client&apos;s view. This can&apos;t be undone.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Keep stage</AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={onConfirm}>
            Delete stage
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
