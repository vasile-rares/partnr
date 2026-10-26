"use client";

import { PlusIcon } from "lucide-react";
import { useState, type FormEvent } from "react";
import { IconButton } from "@/components/actions/IconButton";
import { DateField } from "@/components/forms/DateField";
import { FormError } from "@/components/forms/FormMessage";
import { TextAreaField } from "@/components/forms/TextAreaField";
import { TextField } from "@/components/forms/TextField";
import { useStageRequest } from "@/components/projects/useStageRequest";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { limitsText, type DateLimits } from "@/lib/projects/limits";

type AddStageDialogProps = {
  workspaceId: string;
  projectId: string;
  /** "icon" is the small + at the end of the bar; "button" is for a project with no stage yet. */
  variant: "icon" | "button";
  /** The project's own dates: a stage cannot be due outside them (the API refuses it too). */
  limits: DateLimits;
};

/** A new stage is made in a dialog, like every other new thing in the app. */
export function AddStageDialog({ workspaceId, projectId, variant, limits }: AddStageDialogProps) {
  const range = limitsText(limits);
  const [open, setOpen] = useState(false);
  const [dueDate, setDueDate] = useState("");
  const { pending, message, setMessage, run } = useStageRequest(workspaceId, projectId);

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    run(
      "POST",
      "",
      {
        name: String(data.get("name") ?? ""),
        description: String(data.get("description") ?? ""),
        dueDate: dueDate || null,
      },
      () => {
        setOpen(false);
        setDueDate("");
      },
    );
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        setDueDate("");
        setMessage(undefined);
      }}
    >
      {variant === "icon" ? (
        <IconButton
          icon={PlusIcon}
          tooltip="Add stage"
          aria-label="Add stage"
          variant="outline"
          size="icon-xs"
          className="rounded-full text-muted-foreground"
          onClick={() => setOpen(true)}
        />
      ) : (
        <Button type="button" variant="outline" size="sm" onClick={() => setOpen(true)}>
          <PlusIcon data-icon="inline-start" />
          Add the first stage
        </Button>
      )}
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Add a stage</DialogTitle>
          <DialogDescription>A step your client can follow. With a due date it is placed on the bar by that date.</DialogDescription>
        </DialogHeader>
        <form onSubmit={onSubmit} className="flex flex-col gap-4">
          <TextField label="Stage name" name="name" required maxLength={200} />
          <DateField
            label="Stage due date"
            name="dueDate"
            value={dueDate}
            onChange={setDueDate}
            limits={limits}
            clearable
            hint={range ? `The project runs ${range}.` : undefined}
          />
          <TextAreaField label="Stage description" name="description" maxLength={2000} rows={3} hint="Optional." />
          <FormError message={message} />
          <Button type="submit" disabled={pending}>
            {pending ? "Saving…" : "Add stage"}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  );
}
