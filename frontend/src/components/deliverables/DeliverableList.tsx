"use client";

import { ArchiveIcon, ArchiveRestoreIcon, EyeIcon, EyeOffIcon, PencilIcon, SendIcon, Trash2Icon, Undo2Icon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useRef, useState, useTransition, type FormEvent, type Ref } from "react";
import { IconButton } from "@/components/actions/IconButton";
import { ApprovalRequestForm } from "@/components/deliverables/ApprovalRequestForm";
import { ApprovalSummary } from "@/components/deliverables/ApprovalSummary";
import { DeliverableStatusBadge } from "@/components/deliverables/DeliverableStatusBadge";
import { FormError } from "@/components/forms/FormMessage";
import { SelectField } from "@/components/forms/SelectField";
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
import type { Deliverable } from "@/lib/deliverables/types";
import { formatDate } from "@/lib/format";
import type { Project } from "@/lib/projects/types";

type Props = {
  workspaceId: string;
  project: Project;
  deliverables: Deliverable[];
  isOwner: boolean;
};

type Values = { stageId: string | null; title: string; description: string; url: string };

/** Each change is one request, then the page refreshes. Read-only when the project is archived. */
export function DeliverableList({ workspaceId, project, deliverables, isOwner }: Props) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [editing, setEditing] = useState<string | null>(null);
  const [asking, setAsking] = useState<string | null>(null);
  const [message, setMessage] = useState<string>();
  const addForm = useRef<HTMLFormElement>(null);
  const readOnly = project.archivedAtUtc !== null;
  const base = `/api/workspaces/${workspaceId}/projects/${project.id}/deliverables`;
  const active = deliverables.filter((d) => d.archivedAtUtc === null);
  const archived = deliverables.filter((d) => d.archivedAtUtc !== null);

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

  function row(deliverable: Deliverable) {
    const pendingRound = deliverable.approvals.find((round) => round.status === "Pending");

    return (
      <li key={deliverable.id} className="p-3">
        {editing === deliverable.id ? (
          <DeliverableForm
            project={project}
            deliverable={deliverable}
            pending={pending}
            submitLabel="Save deliverable"
            onSubmit={(values) => run("PUT", `/${deliverable.id}`, values, () => setEditing(null))}
            onCancel={() => setEditing(null)}
          />
        ) : (
          <>
            <DeliverableRow
              deliverable={deliverable}
              pending={pending}
              readOnly={readOnly}
              isOwner={isOwner}
              onEdit={() => setEditing(deliverable.id)}
              onRequestApproval={() => setAsking(deliverable.id)}
              onWithdraw={() => pendingRound && run("POST", `/${deliverable.id}/approvals/${pendingRound.id}/withdraw`)}
              onAction={(action) =>
                action === "delete" ? run("DELETE", `/${deliverable.id}`) : run("POST", `/${deliverable.id}/${action}`)
              }
            />
            {asking === deliverable.id ? (
              <ApprovalRequestForm
                pending={pending}
                onSubmit={(note) => run("POST", `/${deliverable.id}/approvals`, { note }, () => setAsking(null))}
                onCancel={() => setAsking(null)}
              />
            ) : null}
          </>
        )}
      </li>
    );
  }

  return (
    <section aria-labelledby="deliverables-title" className="mb-12 max-w-lg">
      <h2 id="deliverables-title" className="mb-4 font-heading text-xl font-semibold">
        Deliverables
      </h2>

      {active.length === 0 ? (
        <p className="mb-6 text-sm text-muted-foreground">
          {readOnly
            ? "This project has no deliverables."
            : "No deliverables yet. Add a link (a design, a staging site, a document) and share it with your client when it's ready."}
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
          <h3 className="mb-4 font-medium">Add a deliverable</h3>
          <DeliverableForm
            formRef={addForm}
            project={project}
            pending={pending}
            submitLabel="Add deliverable"
            onSubmit={(values) => run("POST", "", values, () => addForm.current?.reset())}
          />
        </div>
      )}
    </section>
  );
}

type Action = "share" | "unshare" | "archive" | "restore" | "delete";

function DeliverableRow({
  deliverable,
  pending,
  readOnly,
  isOwner,
  onEdit,
  onRequestApproval,
  onWithdraw,
  onAction,
}: {
  deliverable: Deliverable;
  pending: boolean;
  readOnly: boolean;
  isOwner: boolean;
  onEdit: () => void;
  onRequestApproval: () => void;
  onWithdraw: () => void;
  onAction: (action: Action) => void;
}) {
  const archivedAt = deliverable.archivedAtUtc;
  const archived = archivedAt !== null;
  const shared = deliverable.status === "Shared";
  const waiting = deliverable.approvalStatus === "Pending";
  // The API refuses both: an open request freezes the details, and a deliverable with approvals is archived, never deleted.
  const deletable = !shared && deliverable.approvals.length === 0;

  return (
    <div className="flex items-start justify-between gap-3">
      <div className="min-w-0 flex-1">
        <p className="font-medium">
          <a href={deliverable.url} target="_blank" rel="noopener noreferrer" className="hover:underline">
            {deliverable.title}
          </a>
        </p>
        {deliverable.description ? <p className="mt-1 text-sm text-muted-foreground">{deliverable.description}</p> : null}
        <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          {archivedAt ? <span>Archived {formatDate(archivedAt.slice(0, 10))}</span> : <DeliverableStatusBadge status={deliverable.status} />}
          {deliverable.stageName ? <span>Stage: {deliverable.stageName}</span> : null}
        </div>
        <ApprovalSummary deliverable={deliverable} />
      </div>
      {readOnly ? null : (
        <div className="flex shrink-0 gap-1">
          {archived ? (
            isOwner ? (
              <IconButton icon={ArchiveRestoreIcon} tooltip="Restore" disabled={pending} aria-label={`Restore ${deliverable.title}`} onClick={() => onAction("restore")} />
            ) : null
          ) : (
            <>
              <IconButton
                icon={shared ? EyeOffIcon : EyeIcon}
                tooltip={shared ? "Unshare" : "Share with client"}
                disabled={pending}
                aria-label={`${shared ? "Unshare" : "Share"} ${deliverable.title}`}
                onClick={() => onAction(shared ? "unshare" : "share")}
              />
              {shared && !waiting ? (
                <IconButton
                  icon={SendIcon}
                  tooltip="Request approval"
                  disabled={pending}
                  aria-label={`Request approval for ${deliverable.title}`}
                  onClick={onRequestApproval}
                />
              ) : null}
              {waiting ? (
                <IconButton
                  icon={Undo2Icon}
                  tooltip="Withdraw request"
                  disabled={pending}
                  aria-label={`Withdraw approval request for ${deliverable.title}`}
                  onClick={onWithdraw}
                />
              ) : (
                <IconButton icon={PencilIcon} tooltip="Edit" disabled={pending} aria-label={`Edit ${deliverable.title}`} onClick={onEdit} />
              )}
              {isOwner ? (
                <IconButton icon={ArchiveIcon} tooltip="Archive" disabled={pending} aria-label={`Archive ${deliverable.title}`} onClick={() => onAction("archive")} />
              ) : null}
              {deletable ? <DeleteButton deliverable={deliverable} disabled={pending} onConfirm={() => onAction("delete")} /> : null}
            </>
          )}
        </div>
      )}
    </div>
  );
}

function DeliverableForm({
  project,
  deliverable,
  pending,
  submitLabel,
  onSubmit,
  onCancel,
  formRef,
}: {
  project: Project;
  deliverable?: Deliverable;
  pending: boolean;
  submitLabel: string;
  onSubmit: (values: Values) => void;
  onCancel?: () => void;
  formRef?: Ref<HTMLFormElement>;
}) {
  // The add form and an open edit form are on screen together, so their labels must differ.
  const names = deliverable
    ? { title: "Title", description: "Description", url: "Link", stage: "Stage" }
    : { title: "Deliverable title", description: "Deliverable description", url: "Deliverable link", stage: "Deliverable stage" };
  const stages = [{ value: "", label: "No stage" }, ...project.stages.map((s) => ({ value: s.id, label: s.name }))];

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    onSubmit({
      stageId: String(data.get("stageId") ?? "") || null,
      title: String(data.get("title") ?? ""),
      description: String(data.get("description") ?? ""),
      url: String(data.get("url") ?? ""),
    });
  }

  return (
    <form ref={formRef} onSubmit={handleSubmit} className="flex flex-col gap-4">
      <TextField label={names.title} name="title" required maxLength={200} defaultValue={deliverable?.title} />
      <TextField
        label={names.url}
        name="url"
        type="url"
        required
        maxLength={2000}
        defaultValue={deliverable?.url}
        hint="A link starting with https://, e.g. a Figma file or a staging site."
      />
      <TextAreaField label={names.description} name="description" maxLength={4000} rows={2} defaultValue={deliverable?.description ?? ""} hint="Optional." />
      <SelectField label={names.stage} name="stageId" defaultValue={deliverable?.stageId ?? ""} options={stages} />
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
function DeleteButton({ deliverable, disabled, onConfirm }: { deliverable: Deliverable; disabled: boolean; onConfirm: () => void }) {
  return (
    <AlertDialog>
      <AlertDialogTrigger
        render={<IconButton icon={Trash2Icon} tooltip="Delete" disabled={disabled} aria-label={`Delete ${deliverable.title}`} />}
      />
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete this deliverable?</AlertDialogTitle>
          <AlertDialogDescription>“{deliverable.title}” is removed from the project. This can&apos;t be undone.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Keep deliverable</AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={onConfirm}>
            Delete deliverable
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
