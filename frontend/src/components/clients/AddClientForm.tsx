"use client";

import { useRouter } from "next/navigation";
import { useActionState, useEffect, useId, useState } from "react";
import {
  Autocomplete,
  AutocompleteContent,
  AutocompleteInput,
  AutocompleteItem,
  AutocompleteList,
} from "@/components/ui/autocomplete";
import { FormError } from "@/components/forms/FormMessage";
import { TextField } from "@/components/forms/TextField";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { getJson, sendJson } from "@/lib/api/client";
import type { Client, ClientSuggestion } from "@/lib/clients/types";

/** The API answers nothing below this many characters, so the form does not even ask. */
const MIN_QUERY_LENGTH = 3;
const DEBOUNCE_MS = 250;

// Good enough to decide when to show the account message; the API validates the address for real.
const LOOKS_LIKE_AN_EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

type Lookup = { query: string; items: ClientSuggestion[]; ok: boolean };

type FormState = {
  message?: string;
  fieldErrors?: Record<string, string>;
  emailTaken?: boolean;
};

type AddClientFormProps = {
  workspaceId: string;
  /** The emails of every client of the workspace, archived included, to warn about a duplicate. */
  existingEmails: string[];
  /** Called once the client is added and invited, so a dialog around the form can close. */
  onDone?: () => void;
};

/** Adds a client from an email and invites them. Existing accounts are offered while typing; choosing one links nothing. */
export function AddClientForm({ workspaceId, existingEmails, onDone }: AddClientFormProps) {
  const router = useRouter();
  const hintId = useId();

  const [email, setEmail] = useState("");
  const [name, setName] = useState("");
  const [lookup, setLookup] = useState<Lookup | null>(null);
  const [open, setOpen] = useState(false);

  const query = email.trim();

  // Asks for suggestions a moment after the typing stops; a request that is no longer needed is cancelled.
  useEffect(() => {
    if (query.length < MIN_QUERY_LENGTH) {
      return;
    }

    const controller = new AbortController();
    const timer = setTimeout(async () => {
      const result = await getJson<ClientSuggestion[]>(
        `/api/workspaces/${workspaceId}/clients/suggestions?email=${encodeURIComponent(query)}`,
        controller.signal,
      );
      if ("aborted" in result) {
        return;
      }
      setLookup({ query, items: result.ok ? result.data : [], ok: result.ok });
    }, DEBOUNCE_MS);

    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [query, workspaceId]);

  const lowered = query.toLowerCase();
  const suggestions =
    query.length >= MIN_QUERY_LENGTH
      ? (lookup?.items ?? []).filter((s) => s.email.toLowerCase().startsWith(lowered))
      : [];

  const complete = LOOKS_LIKE_AN_EMAIL.test(query);
  const alreadyClient = complete && existingEmails.some((e) => e.toLowerCase() === lowered);
  const answer = complete && lookup?.ok && lookup.query === query ? lookup : null;
  const account = answer?.items.find((s) => s.email.toLowerCase() === lowered);

  const [state, submit, pending] = useActionState(
    async (_previous: FormState, formData: FormData): Promise<FormState> => {
      const created = await sendJson<Client>("POST", `/api/workspaces/${workspaceId}/clients`, {
        email: String(formData.get("email") ?? ""),
        name: String(formData.get("name") ?? ""),
      });
      if (!created.ok) {
        return { message: created.message, fieldErrors: created.fieldErrors, emailTaken: created.status === 409 };
      }

      // The card exists now, so a failed invitation is not retried here (that would add the client twice): the client's
      // own page offers "Send invitation" and says what happened.
      const invited = await sendJson<Client>("POST", `/api/workspaces/${workspaceId}/clients/${created.data.id}/invitation`);
      if (!invited.ok) {
        router.push(`/w/${workspaceId}/clients/${created.data.id}?invite=failed`);
      }
      onDone?.();
      router.refresh();

      return {};
    },
    {},
  );

  const errors = state.fieldErrors ?? {};
  const emailError = errors.email ?? (state.emailTaken ? state.message : undefined);
  const hasFieldError = Object.keys(errors).length > 0 || state.emailTaken;

  return (
    <form action={submit} className="flex flex-col gap-6">
      <Field data-invalid={emailError ? true : undefined}>
        <FieldLabel htmlFor={`${hintId}-email`}>Email</FieldLabel>
        <Autocomplete
          name="email"
          mode="none"
          // Open only when there is something to show: an open list hides the rest of the page from assistive technology.
          open={open && suggestions.length > 0}
          onOpenChange={setOpen}
          items={suggestions}
          itemToStringValue={(s) => (s as ClientSuggestion).email}
          value={email}
          onValueChange={(value, details) => {
            setEmail(value);
            if (details.reason === "item-press") {
              const chosen = suggestions.find((s) => s.email === value);
              setName((current) => (current === "" && chosen ? chosen.fullName : current));
            }
          }}
        >
          <AutocompleteInput
            id={`${hintId}-email`}
            type="email"
            required
            maxLength={320}
            autoComplete="off"
            placeholder="name@company.com"
            aria-invalid={emailError ? true : undefined}
            aria-describedby={`${hintId}-hint`}
          />
          <AutocompleteContent>
            <AutocompleteList>
              {(suggestion: ClientSuggestion) => (
                <AutocompleteItem key={suggestion.email} value={suggestion}>
                  <span className="font-medium">{suggestion.fullName}</span>
                  <span className="text-muted-foreground">
                    {suggestion.email}
                    {suggestion.alreadyClient ? " · already a client" : ""}
                  </span>
                </AutocompleteItem>
              )}
            </AutocompleteList>
          </AutocompleteContent>
        </Autocomplete>

        {emailError ? <FieldError>{emailError}</FieldError> : null}
        <FieldDescription id={`${hintId}-hint`} aria-live="polite" className={alreadyClient ? "text-destructive" : undefined}>
          {alreadyClient
            ? "Already a client in this workspace."
            : account
              ? `Has an account: ${account.fullName}. They'll get an invitation to connect.`
              : answer
                ? "No account with this email yet. They'll get an email to create one."
                : "We'll email them an invitation to follow their projects."}
        </FieldDescription>
      </Field>

      <TextField
        label="Name (optional)"
        name="name"
        maxLength={200}
        value={name}
        onChange={(event) => setName(event.target.value)}
        hint="Helps you recognise them until they have an account."
        error={errors.name}
      />

      <FormError message={hasFieldError ? undefined : state.message} />

      <Button type="submit" size="lg" className="w-full" disabled={pending || alreadyClient}>
        {pending ? "Adding client…" : "Add and invite"}
      </Button>
    </form>
  );
}
