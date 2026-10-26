"use client";

import { Trash2Icon } from "lucide-react";
import { useId, useState, type ReactNode } from "react";
import { DatePicker } from "@/components/forms/DatePicker";
import { FormError } from "@/components/forms/FormMessage";
import { StageStatusBadge } from "@/components/projects/StageStatusBadge";
import { useStageRequest } from "@/components/projects/useStageRequest";
import { Button } from "@/components/ui/button";
import { Popover, PopoverContent, PopoverDescription, PopoverHeader, PopoverTitle, PopoverTrigger } from "@/components/ui/popover";
import { Textarea } from "@/components/ui/textarea";
import { formatDate } from "@/lib/format";
import type { DateLimits } from "@/lib/projects/limits";
import { stageStatuses, type Stage, type StageStatus } from "@/lib/projects/types";
import { cn } from "@/lib/utils";

type StagePopoverProps = {
  stage: Stage;
  /** Names the segment for screen readers and the tests: the stage, its status and its date. */
  label: string;
  className?: string;
  /** Present for the freelancer on a project that can still change; without it the stage can only be read. */
  editable?: { workspaceId: string; projectId: string };
  /** The project's own dates: a stage cannot be due outside them (the API refuses it too). */
  limits?: DateLimits;
  children: ReactNode;
};

type Draft = { name: string; description: string; dueDate: string };

const draftOf = (stage: Stage): Draft => ({ name: stage.name, description: stage.description ?? "", dueDate: stage.dueDate ?? "" });

const dotColors: Record<StageStatus, string> = { ToDo: "bg-status-todo", InProgress: "bg-status-doing", Done: "bg-status-done" };

/**
 * One stage of the project bar, opened under it. For the freelancer every field is editable in place and saves as soon as
 * it is left (the status and the date shortcuts at once); the read-only version, for the client, only shows the details.
 */
export function StagePopover({ stage, label, className, editable, limits = { min: null, max: null }, children }: StagePopoverProps) {
  const ids = useId();
  const [open, setOpen] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [saved, setSaved] = useState(false);
  const [nameError, setNameError] = useState<string>();
  const [draft, setDraft] = useState<Draft>(() => draftOf(stage));
  const [seen, setSeen] = useState(stage);
  const { pending, message, run } = useStageRequest(editable?.workspaceId ?? "", editable?.projectId ?? "");

  // What the server says wins for a field whose saved value changed (a drag on the bar, another save), and only for that field.
  if (seen.name !== stage.name || seen.description !== stage.description || seen.dueDate !== stage.dueDate) {
    setSeen(stage);
    setDraft((current) => ({
      name: stage.name !== seen.name ? stage.name : current.name,
      description: stage.description !== seen.description ? (stage.description ?? "") : current.description,
      dueDate: stage.dueDate !== seen.dueDate ? (stage.dueDate ?? "") : current.dueDate,
    }));
  }

  /** Sends the whole draft, so a save never puts back what another field has just changed. */
  function commit(next: Draft = draft, status: StageStatus = stage.status) {
    const body = { name: next.name.trim() || stage.name, description: next.description, dueDate: next.dueDate || null, status };
    const unchanged =
      body.name === stage.name && body.description === (stage.description ?? "") && body.dueDate === stage.dueDate && status === stage.status;
    if (unchanged) {
      return;
    }
    setSaved(false);
    run("PUT", `/${stage.id}`, body, () => setSaved(true));
  }

  function change(patch: Partial<Draft>, saveNow = false) {
    const next = { ...draft, ...patch };
    setDraft(next);
    setSaved(false);
    if (saveNow) {
      commit(next);
    }
  }

  function leaveName() {
    if (draft.name.trim() === "") {
      setNameError("A stage needs a name.");
      setDraft({ ...draft, name: stage.name });
      return;
    }
    setNameError(undefined);
    commit();
  }

  function onOpenChange(next: boolean) {
    if (!next && editable) {
      commit();
      setConfirming(false);
      setNameError(undefined);
    }
    setOpen(next);
  }

  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger
        aria-label={label}
        className={cn("outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2", className)}
      >
        {children}
      </PopoverTrigger>
      {/* Opens centred under the segment, below its label (about 40 px under the bar) so the stage name stays readable. */}
      <PopoverContent align="center" sideOffset={40} className="w-80 gap-3 p-3.5">
        {editable ? (
          <>
            <div>
              <input
                aria-label="Stage name"
                aria-invalid={nameError ? true : undefined}
                value={draft.name}
                maxLength={200}
                onChange={(event) => change({ name: event.target.value })}
                onBlur={leaveName}
                onKeyDown={(event) => event.key === "Enter" && event.currentTarget.blur()}
                className="-mx-1.5 w-[calc(100%+0.75rem)] rounded-md border border-transparent bg-transparent px-1.5 py-0.5 text-base font-semibold outline-none hover:border-input focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 aria-invalid:border-destructive"
              />
              {nameError ? <p className="mt-1 text-xs text-destructive">{nameError}</p> : null}
            </div>

            <div role="group" aria-label={`Status of ${stage.name}`} className="flex gap-0.5 rounded-lg bg-muted p-0.5">
              {stageStatuses.map((status) => {
                const current = stage.status === status.value;
                return (
                  <button
                    key={status.value}
                    type="button"
                    aria-pressed={current}
                    onClick={() => commit(draft, status.value)}
                    className={cn(
                      "flex flex-1 items-center justify-center gap-1.5 rounded-md px-2 py-1 text-xs whitespace-nowrap outline-none focus-visible:ring-2 focus-visible:ring-ring",
                      current ? "bg-background font-medium shadow-sm ring-1 ring-foreground/10" : "text-muted-foreground hover:text-foreground",
                    )}
                  >
                    <span aria-hidden className={cn("size-2 rounded-full", dotColors[status.value])} />
                    {status.label}
                  </button>
                );
              })}
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor={`${ids}-due`} className="text-xs text-muted-foreground">
                Due date
              </label>
              <DatePicker id={`${ids}-due`} value={draft.dueDate} limits={limits} onChange={(value) => change({ dueDate: value }, true)} />
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor={`${ids}-description`} className="text-xs text-muted-foreground">
                Description
              </label>
              <Textarea
                id={`${ids}-description`}
                value={draft.description}
                rows={3}
                maxLength={2000}
                placeholder="Add a description"
                onChange={(event) => change({ description: event.target.value })}
                onBlur={() => commit()}
                className="min-h-16 text-sm"
              />
            </div>

            <FormError message={message} />

            <div className="flex items-center justify-between gap-2 border-t pt-3">
              {confirming ? (
                <>
                  <span className="text-xs whitespace-nowrap">Delete this stage?</span>
                  <div className="flex gap-1">
                    <Button type="button" size="sm" variant="ghost" onClick={() => setConfirming(false)}>
                      Keep stage
                    </Button>
                    <Button type="button" size="sm" variant="destructive" disabled={pending} onClick={() => run("DELETE", `/${stage.id}`, undefined)}>
                      Delete stage
                    </Button>
                  </div>
                </>
              ) : (
                <>
                  <span role="status" className="text-xs text-muted-foreground">
                    {pending ? "Saving…" : saved ? "Saved" : ""}
                  </span>
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    className="text-destructive"
                    aria-label={`Delete ${stage.name}`}
                    onClick={() => setConfirming(true)}
                  >
                    <Trash2Icon data-icon="inline-start" />
                    Delete
                  </Button>
                </>
              )}
            </div>
          </>
        ) : (
          <>
            <PopoverHeader>
              <PopoverTitle>{stage.name}</PopoverTitle>
              <PopoverDescription className="flex flex-wrap items-center gap-2">
                <StageStatusBadge status={stage.status} />
                {stage.dueDate ? <span>Due {formatDate(stage.dueDate)}</span> : <span>No due date</span>}
              </PopoverDescription>
            </PopoverHeader>
            {stage.description ? <p className="whitespace-pre-line text-muted-foreground">{stage.description}</p> : null}
          </>
        )}
      </PopoverContent>
    </Popover>
  );
}
