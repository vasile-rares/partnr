/**
 * Where a client stands with the portal, from the workspace's point of view:
 * - NotInvited: no invitation was sent, or it was cancelled;
 * - Invited: an invitation was sent and can still be accepted;
 * - Expired: the invitation was not accepted in time, so a new one has to be sent;
 * - Active: the client accepted and has an account.
 */
export type PortalStatus = "NotInvited" | "Invited" | "Expired" | "Active";

/** A client as the API returns it. Optional details are null when not set. */
export type Client = {
  id: string;
  /** Null until the client has an account or the workspace gives one; show `displayName(client)` instead. */
  name: string | null;
  email: string;
  company: string | null;
  phone: string | null;
  notes: string | null;
  createdAtUtc: string;
  /** Set when the client is archived (hidden from the default list). */
  archivedAtUtc: string | null;
  portal: {
    status: PortalStatus;
    /** When the open invitation expires (or expired); null when there is none. */
    expiresAtUtc: string | null;
  };
  /**
   * True while an invitation is pending or the client has an account: name, email, phone and company can't be edited
   * here (for a client with an account they are the account's own). Only the notes can.
   */
  detailsLocked: boolean;
};

/** An existing account whose email starts with what was typed in the add-client form. */
export type ClientSuggestion = {
  email: string;
  fullName: string;
  /** The workspace already has a client card with this email. */
  alreadyClient: boolean;
};

/** What to call a client: the name when there is one, otherwise the email address. */
export const displayName = (client: Pick<Client, "name" | "email">): string => client.name ?? client.email;
