"use client";

import { useId } from "react";
import { DatePicker } from "@/components/forms/DatePicker";
import { Field, FieldDescription, FieldLabel } from "@/components/ui/field";

type DateFieldProps = Omit<React.ComponentProps<typeof DatePicker>, "id" | "className"> & {
  label: string;
  hint?: string;
};

/** A labelled date picked on a calendar, built like `TextField`. */
export function DateField({ label, hint, ...picker }: DateFieldProps) {
  const id = useId();

  return (
    <Field>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <DatePicker {...picker} id={id} />
      {hint ? <FieldDescription>{hint}</FieldDescription> : null}
    </Field>
  );
}
