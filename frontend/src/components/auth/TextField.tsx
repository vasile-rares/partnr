import { useId, type ComponentProps } from "react";

type TextFieldProps = Omit<ComponentProps<"input">, "id" | "className"> & {
  label: string;
  /** Always-visible guidance, e.g. a password rule. */
  hint?: string;
  error?: string;
};

export function TextField({ label, hint, error, ...inputProps }: TextFieldProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(" ") || undefined;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      <input
        {...inputProps}
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className="h-11 w-full rounded-md border border-line bg-surface px-3 text-base text-ink outline-offset-2 focus-visible:outline-2 focus-visible:outline-done aria-invalid:border-danger"
      />
      {hint ? (
        <p id={hintId} className="text-sm text-ink-soft">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} className="text-sm text-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
}
