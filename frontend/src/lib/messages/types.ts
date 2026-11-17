// The API's shapes for the project conversations; the firm and the client get the same ones.

export type MessagePreview = {
  authorName: string;
  /** Null when that message was deleted. */
  text: string | null;
  createdAtUtc: string;
};

export type ConversationSummary = {
  projectId: string;
  projectName: string;
  /** Only on the firm's side. */
  clientName: string | null;
  lastMessage: MessagePreview | null;
  unread: number;
};

export type ChatMessage = {
  id: string;
  authorName: string;
  isMine: boolean;
  fromClient: boolean;
  /** Null when deleted. */
  body: string | null;
  createdAtUtc: string;
  editedAtUtc: string | null;
  isDeleted: boolean;
  /** Set only on the reader's own message while it can still be edited or deleted. */
  changeableUntilUtc: string | null;
};

export type Conversation = {
  projectId: string;
  projectName: string;
  clientName: string | null;
  /** Oldest first. */
  messages: ChatMessage[];
  hasEarlier: boolean;
};

export const MESSAGE_MAX_LENGTH = 4000;

/** The API route of a workspace's conversations, the same for the firm and for the client. */
export const conversationsApi = (workspaceId: string) => `/api/workspaces/${workspaceId}/conversations`;
