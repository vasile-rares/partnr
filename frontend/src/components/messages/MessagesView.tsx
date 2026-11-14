import { ChevronLeftIcon } from "lucide-react";
import Link from "next/link";
import type { Conversation, ConversationSummary } from "@/lib/messages/types";
import { cn } from "@/lib/utils";
import { ConversationList } from "./ConversationList";
import { ConversationThread } from "./ConversationThread";

type MessagesViewProps = {
  /** The side's conversations route on the API. */
  apiBase: string;
  /** The side's Messages page, e.g. /w/{id}/messages. */
  hrefBase: string;
  conversations: ConversationSummary[];
  open?: Conversation;
};

/** The list beside the open conversation on a wide screen; on a phone, the list first and the conversation on its own. */
export function MessagesView({ apiBase, hrefBase, conversations, open }: MessagesViewProps) {
  return (
    <div className="messages-view flex h-[calc(100dvh-10rem)] min-h-96 overflow-hidden rounded-xl border">
      <aside className={cn("messages-view-list w-full shrink-0 overflow-y-auto border-r md:block md:w-80", open && "hidden")}>
        <ConversationList apiBase={apiBase} hrefBase={hrefBase} initial={conversations} selectedProjectId={open?.projectId} />
      </aside>
      {open ? (
        <div className="messages-view-thread flex min-w-0 flex-1 flex-col">
          <header className="flex items-center gap-2 border-b px-4 py-3">
            <Link href={hrefBase} className="-ml-2 rounded-md p-1 text-muted-foreground hover:bg-muted md:hidden" aria-label="All conversations">
              <ChevronLeftIcon aria-hidden />
            </Link>
            <span className="flex min-w-0 flex-col">
              <h2 className="truncate font-medium">{open.projectName}</h2>
              {open.clientName ? <span className="text-xs text-muted-foreground">{open.clientName}</span> : null}
            </span>
          </header>
          {/* Keyed by project, so moving to another conversation starts fresh. */}
          <ConversationThread key={open.projectId} api={`${apiBase}/${open.projectId}`} initial={open} />
        </div>
      ) : (
        <p className="hidden flex-1 items-center justify-center p-6 text-sm text-muted-foreground md:flex">
          {conversations.length > 0 ? "Choose a conversation." : "Conversations appear here once a client can see a project."}
        </p>
      )}
    </div>
  );
}
