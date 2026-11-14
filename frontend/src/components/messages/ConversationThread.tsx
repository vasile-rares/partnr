"use client";

import { useCallback, useEffect, useLayoutEffect, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { useConversationChanges } from "@/components/notifications/NotificationsProvider";
import { getJson, sendJson } from "@/lib/api/client";
import { formatDateTimeUtc } from "@/lib/format";
import { MESSAGE_MAX_LENGTH, type ChatMessage, type Conversation } from "@/lib/messages/types";
import { cn } from "@/lib/utils";

type ConversationThreadProps = {
  /** The conversation's own route, e.g. /api/workspaces/{id}/conversations/{projectId}. */
  api: string;
  initial: Conversation;
};

/** One project's conversation: the messages, the box to write in, and the author's own edit / delete while allowed. */
export function ConversationThread({ api, initial }: ConversationThreadProps) {
  // `latest` is the newest page from the API; `earlier` holds what "Load earlier" added before it.
  const [latest, setLatest] = useState(initial.messages);
  const [earlier, setEarlier] = useState<ChatMessage[]>([]);
  const [hasEarlier, setHasEarlier] = useState(initial.hasEarlier);
  const [error, setError] = useState<string>();
  const scroller = useRef<HTMLDivElement>(null);

  const latestIds = new Set(latest.map((m) => m.id));
  const messages = [...earlier.filter((m) => !latestIds.has(m.id)), ...latest];
  const newest = messages.at(-1);

  const reload = useCallback(async () => {
    const result = await getJson<Conversation>(api);
    if (!("aborted" in result) && result.ok) {
      setLatest(result.data.messages);
      setHasEarlier((had) => had || result.data.hasEarlier);
    }
  }, [api]);

  useConversationChanges((projectId) => {
    if (projectId === null || projectId === initial.projectId) {
      void reload();
    }
  });

  // Read up to the newest message shown; the server never moves the mark back, so repeating it is harmless.
  useEffect(() => {
    if (newest) {
      void sendJson("POST", `${api}/read`, { messageId: newest.id });
    }
  }, [api, newest?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  // A new message at the bottom scrolls into view; loading earlier ones keeps the reader where they were.
  useLayoutEffect(() => {
    const box = scroller.current;
    if (box) {
      box.scrollTop = box.scrollHeight;
    }
  }, [newest?.id]);

  async function loadEarlier() {
    const oldest = messages[0];
    if (!oldest) {
      return;
    }
    const result = await getJson<Conversation>(`${api}?before=${oldest.id}`);
    if (!("aborted" in result) && result.ok) {
      setEarlier((current) => [...result.data.messages, ...current]);
      setHasEarlier(result.data.hasEarlier);
    }
  }

  async function send(body: string) {
    const result = await sendJson<ChatMessage>("POST", `${api}/messages`, { body });
    if (!result.ok) {
      setError(result.fieldErrors.body ?? result.message);
      return false;
    }
    setError(undefined);
    setLatest((current) => (current.some((m) => m.id === result.data.id) ? current : [...current, result.data]));
    return true;
  }

  async function change(message: ChatMessage, body: string | null) {
    const result =
      body === null
        ? await sendJson("DELETE", `${api}/messages/${message.id}`)
        : await sendJson("PUT", `${api}/messages/${message.id}`, { body });
    if (!result.ok) {
      setError(result.fieldErrors.body ?? result.message);
    } else {
      setError(undefined);
    }
    await reload();
    return result.ok;
  }

  return (
    <section className="conversation-thread flex min-h-0 flex-1 flex-col" aria-label={`Conversation about ${initial.projectName}`}>
      <div ref={scroller} className="conversation-thread-messages min-h-0 flex-1 overflow-y-auto px-4 py-4">
        {hasEarlier ? (
          <div className="mb-4 flex justify-center">
            <Button type="button" variant="outline" size="sm" onClick={loadEarlier}>
              Load earlier messages
            </Button>
          </div>
        ) : null}
        {messages.length === 0 ? (
          <p className="py-12 text-center text-sm text-muted-foreground">No messages yet. Write the first one below.</p>
        ) : (
          <ol className="conversation-message-list flex flex-col gap-3" role="log" aria-live="polite">
            {messages.map((message) => (
              <MessageItem key={message.id} message={message} onChange={change} />
            ))}
          </ol>
        )}
      </div>
      <div className="conversation-thread-footer border-t px-4 py-3">
        <FormError message={error} />
        <MessageComposer onSend={send} />
      </div>
    </section>
  );
}

function MessageItem({ message, onChange }: { message: ChatMessage; onChange: (message: ChatMessage, body: string | null) => Promise<boolean> }) {
  const [editing, setEditing] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const changeable = message.changeableUntilUtc !== null;

  return (
    <li className={cn("conversation-message flex flex-col gap-1", message.isMine ? "items-end" : "items-start")}>
      <span className="text-xs text-muted-foreground">
        {message.isMine ? "You" : message.authorName} · {formatDateTimeUtc(message.createdAtUtc)}
        {message.editedAtUtc && !message.isDeleted ? " · edited" : null}
      </span>
      {message.isDeleted ? (
        <p className="rounded-lg border border-dashed px-3 py-2 text-sm italic text-muted-foreground">This message was deleted.</p>
      ) : editing ? (
        <EditBox
          initial={message.body ?? ""}
          onCancel={() => setEditing(false)}
          onSave={async (body) => {
            if (await onChange(message, body)) {
              setEditing(false);
            }
          }}
        />
      ) : (
        <p
          className={cn(
            "max-w-[min(36rem,85%)] rounded-lg px-3 py-2 text-sm whitespace-pre-wrap break-words",
            message.isMine ? "bg-primary text-primary-foreground" : "bg-muted",
          )}
        >
          {message.body}
        </p>
      )}
      {changeable && !editing ? (
        confirmingDelete ? (
          <span className="flex items-center gap-2 text-xs">
            Delete this message?
            <Button type="button" variant="destructive" size="xs" onClick={() => void onChange(message, null)}>
              Delete
            </Button>
            <Button type="button" variant="ghost" size="xs" onClick={() => setConfirmingDelete(false)}>
              Keep
            </Button>
          </span>
        ) : (
          <span className="flex gap-1">
            <Button type="button" variant="ghost" size="xs" onClick={() => setEditing(true)} aria-label="Edit your message">
              Edit
            </Button>
            <Button type="button" variant="ghost" size="xs" onClick={() => setConfirmingDelete(true)} aria-label="Delete your message">
              Delete
            </Button>
          </span>
        )
      ) : null}
    </li>
  );
}

function EditBox({ initial, onSave, onCancel }: { initial: string; onSave: (body: string) => Promise<void>; onCancel: () => void }) {
  const [body, setBody] = useState(initial);

  return (
    <form
      className="conversation-message-edit flex w-full max-w-[min(36rem,85%)] flex-col gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        void onSave(body);
      }}
    >
      <Textarea
        aria-label="Edit your message"
        value={body}
        maxLength={MESSAGE_MAX_LENGTH}
        onChange={(event) => setBody(event.target.value)}
        onKeyDown={(event) => event.key === "Escape" && onCancel()}
        autoFocus
      />
      <span className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" size="sm" disabled={body.trim().length === 0}>
          Save
        </Button>
      </span>
    </form>
  );
}

function MessageComposer({ onSend }: { onSend: (body: string) => Promise<boolean> }) {
  const [body, setBody] = useState("");
  const [sending, setSending] = useState(false);

  async function submit(event?: FormEvent) {
    event?.preventDefault();
    if (sending || body.trim().length === 0) {
      return;
    }
    setSending(true);
    if (await onSend(body)) {
      setBody("");
    }
    setSending(false);
  }

  // Enter sends, Shift+Enter starts a new line (and nothing is sent while an input method is composing a word).
  function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault();
      void submit();
    }
  }

  return (
    <form onSubmit={submit} className="message-composer flex items-end gap-2">
      <Textarea
        aria-label="Write a message"
        placeholder="Write a message…"
        value={body}
        maxLength={MESSAGE_MAX_LENGTH}
        rows={2}
        onChange={(event) => setBody(event.target.value)}
        onKeyDown={onKeyDown}
        className="min-h-0 resize-none"
      />
      <Button type="submit" disabled={sending || body.trim().length === 0}>
        Send
      </Button>
    </form>
  );
}
