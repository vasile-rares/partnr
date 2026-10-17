import { useId, type ComponentProps } from "react";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";

type TextFieldProps = Omit<ComponentProps<"input">, "id" | "className"> & {
  label: string;
  hint?: string;
  error?: string;
};

/** A labelled input built from the shadcn Field parts, with the hint and error wired to the input for screen readers. */
export function TextField({ label, hint, error, ...inputProps }: TextFieldProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(" ") || undefined;

  return (
    <Field data-invalid={error ? true : undefined}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Input {...inputProps} id={id} aria-invalid={error ? true : undefined} aria-describedby={describedBy} />
      {hint ? <FieldDescription id={hintId}>{hint}</FieldDescription> : null}
      {error ? <FieldError id={errorId}>{error}</FieldError> : null}
    </Field>
  );
}
