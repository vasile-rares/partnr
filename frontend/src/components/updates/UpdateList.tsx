"use client";

import { ArchiveIcon, ArchiveRestoreIcon, EyeIcon, EyeOffIcon, PencilIcon, Trash2Icon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useRef, useState, useTransition, type FormEvent, type Ref } from "react";
import { IconButton } from "@/components/actions/IconButton";
import { DeliverableStatusBadge } from "@/components/deliverables/DeliverableStatusBadge";
import { FormError } from "@/components/forms/FormMessage";
import { TextAreaField } from "@/components/forms/TextAreaField";
import { TextField } from "@/components/forms/TextField";
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
import type { Project } from "@/lib/projects/types";
import type { ProjectUpdate } from "@/lib/updates/types";

type Props = {
  workspaceId: string;
  project: Project;
  updates: ProjectUpdate[];
  isOwner: boolean;
};

type Values = { title: string; body: string };

/** Each change is one request, then the page refreshes. Read-only when the project is archived. */
export function UpdateList({ workspaceId, project, updates, isOwner }: Props) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [editing, setEditing] = useState<string | null>(null);
  const [message, setMessage] = useState<string>();
  const addForm = useRef<HTMLFormElement>(null);
  const readOnly = project.archivedAtUtc !== null;
  const base = `/api/workspaces/${workspaceId}/projects/${project.id}/updates`;
  const active = updates.filter((u) => u.archivedAtUtc === null);
  const archived = updates.filter((u) => u.archivedAtUtc !== null);

  function run(method: "POST" | "PUT" | "DELETE", path: string, body?: unknown, onDone?: () => void) {
    startTransition(async () => {
      setMessage(undefined);
      const result = await sendJson(method, `${base}${path}`, body);
      if (result.ok) {
        onDone?.();
        router.refresh();
      } else {
        setMessage(result.message);
      }
    });
  }

  function row(update: ProjectUpdate) {
    return (
      <li key={update.id} className="p-3">
        {editing === update.id ? (
          <UpdateForm
            update={update}
            pending={pending}
            submitLabel="Save update"
            onSubmit={(values) => run("PUT", `/${update.id}`, values, () => setEditing(null))}
            onCancel={() => setEditing(null)}
          />
        ) : (
          <UpdateRow
            update={update}
            pending={pending}
            readOnly={readOnly}
            isOwner={isOwner}
            onEdit={() => setEditing(update.id)}
            onAction={(action) => (action === "delete" ? run("DELETE", `/${update.id}`) : run("POST", `/${update.id}/${action}`))}
          />
        )}
      </li>
    );
  }

  return (
    <section aria-labelledby="updates-title" className="mb-12 max-w-lg">
      <h2 id="updates-title" className="mb-4 font-heading text-xl font-semibold">
        Updates
      </h2>

      {active.length === 0 ? (
        <p className="mb-6 text-sm text-muted-foreground">
          {readOnly
            ? "This project has no updates."
            : "No updates yet. Write a short note about where the project stands and share it with your client when it's ready."}
        </p>
      ) : (
        <ul className="mb-6 flex flex-col divide-y rounded-lg border">{active.map(row)}</ul>
      )}

      {archived.length > 0 ? (
        <div className="mb-6">
          <h3 className="mb-2 text-sm font-medium text-muted-foreground">Archived</h3>
          <ul className="flex flex-col divide-y rounded-lg border">{archived.map(row)}</ul>
        </div>
      ) : null}

      <FormError message={message} />

      {readOnly ? null : (
        <div className="mt-4 rounded-lg border p-4">
          <h3 className="mb-4 font-medium">Add an update</h3>
          <UpdateForm
            formRef={addForm}
            pending={pending}
            submitLabel="Add update"
            onSubmit={(values) => run("POST", "", values, () => addForm.current?.reset())}
          />
        </div>
      )}
    </section>
  );
}

type Action = "share" | "unshare" | "archive" | "restore" | "delete";

function UpdateRow({
  update,
  pending,
  readOnly,
  isOwner,
  onEdit,
  onAction,
}: {
  update: ProjectUpdate;
  pending: boolean;
  readOnly: boolean;
  isOwner: boolean;
  onEdit: () => void;
  onAction: (action: Action) => void;
}) {
  const archivedAt = update.archivedAtUtc;
  const shared = update.status === "Shared";

  return (
    <div className="flex items-start justify-between gap-3">
      <div className="min-w-0 flex-1">
        <p className="font-medium">{update.title}</p>
        {update.body ? <p className="mt-1 whitespace-pre-line text-sm text-muted-foreground">{update.body}</p> : null}
        <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          {archivedAt ? <span>Archived {formatDate(archivedAt.slice(0, 10))}</span> : <DeliverableStatusBadge status={update.status} />}
          {shared && update.sharedAtUtc ? <span>Shared {formatDate(update.sharedAtUtc.slice(0, 10))}</span> : null}
        </div>
      </div>
      {readOnly ? null : (
        <div className="flex shrink-0 gap-1">
          {archivedAt ? (
            isOwner ? (
              <IconButton icon={ArchiveRestoreIcon} tooltip="Restore" disabled={pending} aria-label={`Restore ${update.title}`} onClick={() => onAction("restore")} />
            ) : null
          ) : (
            <>
              <IconButton
                icon={shared ? EyeOffIcon : EyeIcon}
                tooltip={shared ? "Unshare" : "Share with client"}
                disabled={pending}
                aria-label={`${shared ? "Unshare" : "Share"} ${update.title}`}
                onClick={() => onAction(shared ? "unshare" : "share")}
              />
              <IconButton icon={PencilIcon} tooltip="Edit" disabled={pending} aria-label={`Edit ${update.title}`} onClick={onEdit} />
              {isOwner ? (
                <IconButton icon={ArchiveIcon} tooltip="Archive" disabled={pending} aria-label={`Archive ${update.title}`} onClick={() => onAction("archive")} />
              ) : null}
              {shared ? null : <DeleteButton update={update} disabled={pending} onConfirm={() => onAction("delete")} />}
            </>
          )}
        </div>
      )}
    </div>
  );
}

function UpdateForm({
  update,
  pending,
  submitLabel,
  onSubmit,
  onCancel,
  formRef,
}: {
  update?: ProjectUpdate;
  pending: boolean;
  submitLabel: string;
  onSubmit: (values: Values) => void;
  onCancel?: () => void;
  formRef?: Ref<HTMLFormElement>;
}) {
  // The add form and an open edit form are on screen together, so their labels must differ.
  const names = update ? { title: "Title", body: "Details" } : { title: "Update title", body: "What's happening now?" };

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    onSubmit({ title: String(data.get("title") ?? ""), body: String(data.get("body") ?? "") });
  }

  return (
    <form ref={formRef} onSubmit={handleSubmit} className="flex flex-col gap-4">
      <TextField label={names.title} name="title" required maxLength={200} defaultValue={update?.title} />
      <TextAreaField label={names.body} name="body" maxLength={4000} rows={3} defaultValue={update?.body ?? ""} hint="Optional." />
      <div className="flex gap-2">
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : submitLabel}
        </Button>
        {onCancel ? (
          <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
            Cancel
          </Button>
        ) : null}
      </div>
    </form>
  );
}

// Only a draft can be deleted, and it cannot be undone, so it asks first.
function DeleteButton({ update, disabled, onConfirm }: { update: ProjectUpdate; disabled: boolean; onConfirm: () => void }) {
  return (
    <AlertDialog>
      <AlertDialogTrigger
        render={<IconButton icon={Trash2Icon} tooltip="Delete" disabled={disabled} aria-label={`Delete ${update.title}`} />}
      />
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete this update?</AlertDialogTitle>
          <AlertDialogDescription>“{update.title}” is removed from the project. This can&apos;t be undone.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Keep update</AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={onConfirm}>
            Delete update
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
