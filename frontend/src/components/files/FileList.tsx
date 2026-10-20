"use client";

import { ArchiveIcon, ArchiveRestoreIcon, DownloadIcon, EyeIcon, EyeOffIcon, PencilIcon, Trash2Icon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useRef, useState, useTransition, type FormEvent, type Ref } from "react";
import { IconButton, IconLink } from "@/components/actions/IconButton";
import { DeliverableStatusBadge } from "@/components/deliverables/DeliverableStatusBadge";
import { FileDropzone } from "@/components/files/FileDropzone";
import { FileTypeIcon } from "@/components/files/FileTypeIcon";
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
import { sendForm, sendJson } from "@/lib/api/client";
import { MAX_FILE_BYTES, type ProjectFile } from "@/lib/files/types";
import { formatDate, formatFileSize } from "@/lib/format";
import type { Project } from "@/lib/projects/types";

type Props = {
  workspaceId: string;
  project: Project;
  files: ProjectFile[];
  isOwner: boolean;
};

type Values = { name: string; description: string };

/** Each change is one request, then the page refreshes. Read-only when the project is archived. */
export function FileList({ workspaceId, project, files, isOwner }: Props) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [editing, setEditing] = useState<string | null>(null);
  const [message, setMessage] = useState<string>();
  const addForm = useRef<HTMLFormElement>(null);
  const readOnly = project.archivedAtUtc !== null;
  const base = `/api/workspaces/${workspaceId}/projects/${project.id}/files`;
  const active = files.filter((f) => f.archivedAtUtc === null);
  const archived = files.filter((f) => f.archivedAtUtc !== null);

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

  function upload(form: FormData) {
    const file = form.get("file");
    if (file instanceof File && file.size > MAX_FILE_BYTES) {
      setMessage(`${file.name} is larger than ${formatFileSize(MAX_FILE_BYTES)}.`);
      return;
    }

    startTransition(async () => {
      setMessage(undefined);
      const result = await sendForm(base, form);
      if (result.ok) {
        addForm.current?.reset();
        router.refresh();
      } else {
        setMessage(result.message);
      }
    });
  }

  function row(file: ProjectFile) {
    return (
      <li key={file.id} className="p-3">
        {editing === file.id ? (
          <FileEditForm
            file={file}
            pending={pending}
            onSubmit={(values) => run("PUT", `/${file.id}`, values, () => setEditing(null))}
            onCancel={() => setEditing(null)}
          />
        ) : (
          <FileRow
            file={file}
            downloadUrl={`${base}/${file.id}/download`}
            pending={pending}
            readOnly={readOnly}
            isOwner={isOwner}
            onEdit={() => setEditing(file.id)}
            onAction={(action) => (action === "delete" ? run("DELETE", `/${file.id}`) : run("POST", `/${file.id}/${action}`))}
          />
        )}
      </li>
    );
  }

  return (
    <section aria-labelledby="files-title" className="mb-12 max-w-lg">
      <h2 id="files-title" className="mb-4 font-heading text-xl font-semibold">
        Files
      </h2>

      {readOnly ? null : <UploadForm formRef={addForm} pending={pending} onSubmit={upload} />}

      <FormError message={message} />

      {active.length === 0 ? (
        <p className="mb-6 text-sm text-muted-foreground">
          {readOnly ? "This project has no files." : "No files yet. Share a file with your client once it's ready."}
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

    </section>
  );
}

type Action = "share" | "unshare" | "archive" | "restore" | "delete";

function FileRow({
  file,
  downloadUrl,
  pending,
  readOnly,
  isOwner,
  onEdit,
  onAction,
}: {
  file: ProjectFile;
  downloadUrl: string;
  pending: boolean;
  readOnly: boolean;
  isOwner: boolean;
  onEdit: () => void;
  onAction: (action: Action) => void;
}) {
  const archivedAt = file.archivedAtUtc;
  const shared = file.status === "Shared";

  return (
    <div className="flex items-start justify-between gap-3">
      <div className="flex min-w-0 flex-1 gap-3">
        <FileTypeIcon fileName={file.fileName} contentType={file.contentType} />
        <div className="min-w-0">
          <div className="mb-1.5 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            {archivedAt ? <span>Archived {formatDate(archivedAt.slice(0, 10))}</span> : <DeliverableStatusBadge status={file.status} />}
            <span className="break-all">
              {file.fileName} · {formatFileSize(file.sizeBytes)}
            </span>
          </div>
          <p className="break-words font-medium">{file.name}</p>
          {file.description ? <p className="mt-1 whitespace-pre-line text-sm text-muted-foreground">{file.description}</p> : null}
        </div>
      </div>
      <div className="flex shrink-0 gap-1">
        {/* A plain link: the API answers with an attachment, so the browser saves it instead of navigating away. */}
        <IconLink href={downloadUrl} download icon={DownloadIcon} tooltip="Download" aria-label={`Download ${file.name}`} />
        {readOnly ? null : archivedAt ? (
          isOwner ? (
            <IconButton icon={ArchiveRestoreIcon} tooltip="Restore" disabled={pending} aria-label={`Restore ${file.name}`} onClick={() => onAction("restore")} />
          ) : null
        ) : (
          <>
            <IconButton
              icon={shared ? EyeOffIcon : EyeIcon}
              tooltip={shared ? "Unshare" : "Share with client"}
              disabled={pending}
              aria-label={`${shared ? "Unshare" : "Share"} ${file.name}`}
              onClick={() => onAction(shared ? "unshare" : "share")}
            />
            <IconButton icon={PencilIcon} tooltip="Edit" disabled={pending} aria-label={`Edit ${file.name}`} onClick={onEdit} />
            {isOwner ? (
              <IconButton icon={ArchiveIcon} tooltip="Archive" disabled={pending} aria-label={`Archive ${file.name}`} onClick={() => onAction("archive")} />
            ) : null}
            {shared ? null : <DeleteButton file={file} disabled={pending} onConfirm={() => onAction("delete")} />}
          </>
        )}
      </div>
    </div>
  );
}

function UploadForm({
  pending,
  onSubmit,
  formRef,
}: {
  pending: boolean;
  onSubmit: (form: FormData) => void;
  formRef: Ref<HTMLFormElement>;
}) {
  const [file, setFile] = useState<File | null>(null);
  const hintId = "upload-file-hint";

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onSubmit(new FormData(event.currentTarget));
  }

  // The edit form can be open at the same time, so these labels differ from its "Name" and "Description".
  return (
    <form ref={formRef} onSubmit={handleSubmit} onReset={() => setFile(null)} className="mb-6 flex flex-col gap-4">
      <FileDropzone name="file" describedBy={hintId} hint={`Up to ${formatFileSize(MAX_FILE_BYTES)}.`} file={file} onFileChange={setFile} />
      {file ? (
        <>
          <TextField label="Name (optional)" name="name" maxLength={200} placeholder={file.name} />
          <TextAreaField label="Note (optional)" name="description" maxLength={2000} rows={2} />
          <div className="flex gap-2">
            <Button type="submit" disabled={pending}>
              {pending ? "Uploading…" : "Upload file"}
            </Button>
            <Button type="reset" variant="outline" disabled={pending}>
              Cancel
            </Button>
          </div>
        </>
      ) : null}
    </form>
  );
}

function FileEditForm({
  file,
  pending,
  onSubmit,
  onCancel,
}: {
  file: ProjectFile;
  pending: boolean;
  onSubmit: (values: Values) => void;
  onCancel: () => void;
}) {
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    onSubmit({ name: String(data.get("name") ?? ""), description: String(data.get("description") ?? "") });
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <TextField label="Name" name="name" required maxLength={200} defaultValue={file.name} />
      <TextAreaField label="Description" name="description" maxLength={2000} rows={2} defaultValue={file.description ?? ""} hint="Optional." />
      <div className="flex gap-2">
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : "Save file"}
        </Button>
        <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

// Only a draft can be deleted, and it cannot be undone, so it asks first.
function DeleteButton({ file, disabled, onConfirm }: { file: ProjectFile; disabled: boolean; onConfirm: () => void }) {
  return (
    <AlertDialog>
      <AlertDialogTrigger
        render={<IconButton icon={Trash2Icon} tooltip="Delete" disabled={disabled} aria-label={`Delete ${file.name}`} />}
      />
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete this file?</AlertDialogTitle>
          <AlertDialogDescription>“{file.name}” is removed from the project for good. This can&apos;t be undone.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Keep file</AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={onConfirm}>
            Delete file
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
