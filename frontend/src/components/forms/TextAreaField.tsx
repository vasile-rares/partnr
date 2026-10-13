import { useId, type ComponentProps } from "react";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Textarea } from "@/components/ui/textarea";

type TextAreaFieldProps = Omit<ComponentProps<"textarea">, "id" | "className"> & {
  label: string;
  hint?: string;
  error?: string;
};

/** The multi-line sibling of TextField, wired the same way for screen readers. */
export function TextAreaField({ label, hint, error, ...textareaProps }: TextAreaFieldProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(" ") || undefined;

  return (
    <Field data-invalid={error ? true : undefined}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Textarea {...textareaProps} id={id} aria-invalid={error ? true : undefined} aria-describedby={describedBy} />
      {hint ? <FieldDescription id={hintId}>{hint}</FieldDescription> : null}
      {error ? <FieldError id={errorId}>{error}</FieldError> : null}
    </Field>
  );
}
