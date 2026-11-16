"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { DateField } from "@/components/forms/DateField";
import { FormError } from "@/components/forms/FormMessage";
import { SelectField } from "@/components/forms/SelectField";
import { TextAreaField } from "@/components/forms/TextAreaField";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";
import { fromLocalDate } from "@/lib/format";
import { clockOf, DEFAULT_DURATION, DURATIONS, durationLabel, durationOf, isPast, localDateOf, localTimeOf, momentsOf } from "@/lib/meetings/time";
import type { Meeting } from "@/lib/meetings/types";

export type MeetingProjectOption = { id: string; name: string; clientId: string };

type MeetingFormProps = {
  workspaceId: string;
  /** Active clients only: an archived one cannot get a new meeting. */
  clients: { id: string; displayName: string }[];
  /** Projects of any client that can still be talked about; the form offers the chosen client's. */
  projects: MeetingProjectOption[];
  /** Editing: the client is fixed. Creating: the client already chosen (e.g. coming from a client's page). */
  meeting?: Meeting;
  clientId?: string;
  onDone: () => void;
};

/** Schedules a meeting (choosing the client) or changes one (the client cannot change). The date is picked, the time and the length are chosen; the reader's own time zone is used. */
export function MeetingForm({ workspaceId, clients, projects, meeting, clientId, onDone }: MeetingFormProps) {
  const router = useRouter();
  const editing = meeting !== undefined;
  const [client, setClient] = useState(meeting?.client.id ?? clientId ?? "");
  const [projectId, setProjectId] = useState(meeting?.project?.id ?? "");
  const [title, setTitle] = useState(meeting?.title ?? "");
  const [description, setDescription] = useState(meeting?.description ?? "");
  const [date, setDate] = useState(meeting ? localDateOf(meeting.startsAtUtc) : "");
  const [time, setTime] = useState(meeting ? localTimeOf(meeting.startsAtUtc) : "10:00");
  const [minutes, setMinutes] = useState(meeting ? durationOf(meeting) : DEFAULT_DURATION);
  const [pending, setPending] = useState(false);
  const [message, setMessage] = useState<string>();
  const [errors, setErrors] = useState<Record<string, string>>({});

  const moments = momentsOf(date, time, minutes);
  const durations = DURATIONS.includes(minutes) ? DURATIONS : [...DURATIONS, minutes].sort((a, b) => a - b);
  const clientProjects = projects.filter((project) => project.clientId === client);
  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(undefined);
    setErrors({});

    if (!moments) {
      setMessage("Pick a date and a start time.");
      return;
    }
    if (isPast(moments.startsAt) && (!editing || moments.startsAt !== meeting.startsAtUtc)) {
      setMessage("A meeting has to start in the future.");
      return;
    }

    setPending(true);
    const details = { projectId: projectId || null, title, description, ...moments };
    const base = `/api/workspaces/${workspaceId}/meetings`;
    const result = editing
      ? await sendJson("PUT", `${base}/${meeting.id}`, details)
      : await sendJson("POST", base, { ...details, clientId: client });
    setPending(false);

    if (result.ok) {
      router.refresh();
      onDone();
    } else {
      setMessage(Object.keys(result.fieldErrors).length > 0 ? undefined : result.message);
      setErrors(result.fieldErrors);
    }
  }

  return (
    <form onSubmit={submit} className="meeting-form flex flex-col gap-5">
      <fieldset disabled={pending} className="meeting-form-fields flex flex-col gap-5">
        {editing ? (
          <div className="meeting-form-client text-sm">
            <p className="font-medium">Client</p>
            <p className="mt-1 text-muted-foreground">{meeting.client.displayName}</p>
          </div>
        ) : (
          <SelectField
            label="Client"
            required
            value={client}
            onChange={(event) => {
              setClient(event.target.value);
              setProjectId("");
            }}
            options={[{ value: "", label: "Choose a client…" }, ...clients.map((c) => ({ value: c.id, label: c.displayName }))]}
            hint="Google invites them by email, and they see the meeting in their portal."
            error={errors.clientid}
          />
        )}
        <SelectField
          label="Project"
          value={projectId}
          onChange={(event) => setProjectId(event.target.value)}
          options={[{ value: "", label: "No project" }, ...clientProjects.map((p) => ({ value: p.id, label: p.name }))]}
          hint="Optional."
          error={errors.projectid}
        />
        <TextField
          label="Title"
          required
          maxLength={200}
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          error={errors.title}
        />
        <DateField
          label="Date"
          value={date}
          onChange={setDate}
          limits={{ min: fromLocalDate(new Date()), max: null }}
          hint={errors.startsat ?? errors.endsat}
        />
        <div className="meeting-form-time grid gap-5 sm:grid-cols-2">
          <TextField label="Start time" type="time" required value={time} onChange={(event) => setTime(event.target.value)} />
          <SelectField
            label="Length"
            value={String(minutes)}
            onChange={(event) => setMinutes(Number(event.target.value))}
            options={durations.map((m) => ({ value: String(m), label: durationLabel(m) }))}
          />
        </div>
        <p className="meeting-form-summary -mt-2 text-sm text-muted-foreground">
          {moments ? `Ends at ${clockOf(moments.endsAt)}. ` : null}
          Times are in your time zone ({timeZone}).
        </p>
        <TextAreaField
          label="Notes"
          maxLength={2000}
          rows={3}
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          hint="Optional. Your client sees them in the invitation and in their portal."
          error={errors.description}
        />
      </fieldset>

      <FormError message={message} />
      <Button type="submit" size="lg" className="w-full" disabled={pending}>
        {pending ? "Saving…" : editing ? "Save meeting" : "Schedule meeting"}
      </Button>
    </form>
  );
}
