"use client";

import Link from "next/link";
import { useState } from "react";
import { Badge } from "@/components/reui/badge";
import { useConversationChanges } from "@/components/notifications/NotificationsProvider";
import { getJson } from "@/lib/api/client";
import { formatDateTimeUtc } from "@/lib/format";
import type { ConversationSummary } from "@/lib/messages/types";
import { cn } from "@/lib/utils";

type ConversationListProps = {
  apiBase: string;
  hrefBase: string;
  initial: ConversationSummary[];
  selectedProjectId?: string;
};

/** Every conversation the reader can open, latest first, with its unread count; it follows changes live. */
export function ConversationList({ apiBase, hrefBase, initial, selectedProjectId }: ConversationListProps) {
  const [conversations, setConversations] = useState(initial);

  useConversationChanges(async () => {
    const result = await getJson<ConversationSummary[]>(apiBase);
    if (!("aborted" in result) && result.ok) {
      setConversations(result.data);
    }
  });

  if (conversations.length === 0) {
    return (
      <p className="conversation-list-empty p-4 text-sm text-muted-foreground">
        No conversations yet. One opens for each project as soon as the client can see it.
      </p>
    );
  }

  return (
    <ul className="conversation-list flex flex-col divide-y" aria-label="Conversations">
      {conversations.map((conversation) => {
        const selected = conversation.projectId === selectedProjectId;
        const last = conversation.lastMessage;
        return (
          <li key={conversation.projectId} className="conversation-list-item">
            <Link
              href={`${hrefBase}/${conversation.projectId}`}
              aria-current={selected ? "page" : undefined}
              className={cn("flex flex-col gap-1 px-4 py-3 hover:bg-muted", selected && "bg-muted")}
            >
              <span className="flex items-center justify-between gap-2">
                <span className={cn("truncate", conversation.unread > 0 ? "font-semibold" : "font-medium")}>{conversation.projectName}</span>
                {conversation.unread > 0 ? (
                  <Badge aria-label={`${conversation.unread} unread`}>{conversation.unread}</Badge>
                ) : null}
              </span>
              {conversation.clientName ? <span className="text-xs text-muted-foreground">{conversation.clientName}</span> : null}
              <span className="truncate text-sm text-muted-foreground">
                {last ? (
                  <>
                    {last.authorName}: {last.text ?? <em>message deleted</em>}
                  </>
                ) : (
                  "No messages yet"
                )}
              </span>
              {last ? <span className="text-xs text-muted-foreground">{formatDateTimeUtc(last.createdAtUtc)}</span> : null}
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
