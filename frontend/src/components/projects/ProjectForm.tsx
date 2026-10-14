"use client";

import { useRouter } from "next/navigation";
import { useActionState } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { SelectField } from "@/components/forms/SelectField";
import { TextAreaField } from "@/components/forms/TextAreaField";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import { projectStatuses, type Project, type ProjectStatus } from "@/lib/projects/types";

type Values = { clientId: string; name: string; description: string; startDate: string; dueDate: string; status: ProjectStatus };

type FormState = {
  /** Kept so a failed attempt does not wipe what was typed. */
  values: Values;
  saved?: boolean;
  message?: string;
  fieldErrors?: Record<string, string>;
};

type ProjectFormProps = {
  workspaceId: string;
} & (
  | {
      /** Creating: the client to pick from, and the one already chosen (e.g. coming from a client's page). */
      project?: undefined;
      clients: { id: string; displayName: string }[];
      clientId?: string;
    }
  | { project: Project; clients?: undefined; clientId?: undefined }
);

const valuesOf = (project: Project): Values => ({
  clientId: project.client.id,
  name: project.name,
  description: project.description ?? "",
  startDate: project.startDate ?? "",
  dueDate: project.dueDate ?? "",
  status: project.status,
});

/** Creates a project (choosing its client) or edits one (the client can no longer change, the status can). */
export function ProjectForm({ workspaceId, project, clients, clientId }: ProjectFormProps) {
  const router = useRouter();
  const editing = project !== undefined;
  // An archived project is read-only until it is restored; the API refuses changes too.
  const readOnly = editing && project.archivedAtUtc !== null;

  const [state, submit, pending] = useActionState(
    async (_previous: FormState, formData: FormData): Promise<FormState> => {
      const text = (key: keyof Values) => String(formData.get(key) ?? "");
      const values: Values = {
        clientId: editing ? project.client.id : text("clientId"),
        name: text("name"),
        description: text("description"),
        startDate: text("startDate"),
        dueDate: text("dueDate"),
        status: editing ? (text("status") as ProjectStatus) : "Planning",
      };
      const details = {
        name: values.name,
        description: values.description,
        // An empty date input means "no date".
        startDate: values.startDate || null,
        dueDate: values.dueDate || null,
      };

      if (!editing) {
        const result = await sendJson<Project>("POST", `/api/workspaces/${workspaceId}/projects`, {
          ...details,
          clientId: values.clientId,
        });
        if (result.ok) {
          router.replace(`/w/${workspaceId}/projects/${result.data.id}`);
          return { values };
        }
        return { values, message: result.message, fieldErrors: result.fieldErrors };
      }

      const result = await sendJson<Project>("PUT", `/api/workspaces/${workspaceId}/projects/${project.id}`, {
        ...details,
        status: values.status,
      });
      if (result.ok) {
        // Refresh so the title and everything else showing the project pick up the change.
        router.refresh();
        return { values: valuesOf(result.data), saved: true };
      }
      return { values, message: result.message, fieldErrors: result.fieldErrors };
    },
    editing
      ? { values: valuesOf(project) }
      : { values: { clientId: clientId ?? "", name: "", description: "", startDate: "", dueDate: "", status: "Planning" } },
  );

  const errors = state.fieldErrors ?? {};
  const hasFieldError = Object.keys(errors).length > 0;

  return (
    <form action={submit} className="flex max-w-lg flex-col gap-6">
      <fieldset disabled={readOnly || pending} className="flex flex-col gap-6">
        {editing ? null : (
          <SelectField
            label="Client"
            name="clientId"
            required
            defaultValue={state.values.clientId}
            options={[
              { value: "", label: "Choose a client…" },
              ...clients.map((client) => ({ value: client.id, label: client.displayName })),
            ]}
            hint="The client can't be changed later: the project shows up in their portal."
            error={errors.clientid}
          />
        )}
        <TextField label="Name" name="name" required maxLength={200} defaultValue={state.values.name} error={errors.name} />
        <TextAreaField
          label="Description"
          name="description"
          maxLength={4000}
          rows={4}
          defaultValue={state.values.description}
          hint="Optional. Your client will see it in their portal."
          error={errors.description}
        />
        <div className="grid gap-6 sm:grid-cols-2">
          <TextField
            label="Start date"
            name="startDate"
            type="date"
            defaultValue={state.values.startDate}
            error={errors.startdate}
          />
          <TextField label="Due date" name="dueDate" type="date" defaultValue={state.values.dueDate} error={errors.duedate} />
        </div>
        {editing ? (
          <SelectField
            label="Status"
            name="status"
            required
            defaultValue={state.values.status}
            options={projectStatuses}
            hint="Your client sees the project once it leaves Planning."
            error={errors.status}
          />
        ) : null}
      </fieldset>

      <FormError message={hasFieldError ? undefined : state.message} />
      {state.saved ? (
        <p role="status" className="text-sm text-status-done">
          Changes saved.
        </p>
      ) : null}

      {readOnly ? null : (
        <Button type="submit" size="lg" className="w-full" disabled={pending}>
          {editing ? (pending ? "Saving…" : "Save changes") : pending ? "Creating…" : "Create project"}
        </Button>
      )}
    </form>
  );
}
