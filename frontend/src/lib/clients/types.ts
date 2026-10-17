/** NotInvited also covers a cancelled invitation; Expired means a new one has to be sent. */
export type PortalStatus = "NotInvited" | "Invited" | "Expired" | "Active";

export type Client = {
  id: string;
  /** Null until the client has an account or the workspace gives one; show `displayName(client)` instead. */
  name: string | null;
  email: string;
  company: string | null;
  phone: string | null;
  notes: string | null;
  createdAtUtc: string;
  archivedAtUtc: string | null;
  portal: {
    status: PortalStatus;
    expiresAtUtc: string | null;
  };
  /** Name, email, phone and company can't be edited here while an invitation is pending or once the client has an account. */
  detailsLocked: boolean;
};

export type ClientSuggestion = {
  email: string;
  fullName: string;
  alreadyClient: boolean;
};

/** What to call a client: the name when there is one, otherwise the email address. */
export const displayName = (client: Pick<Client, "name" | "email">): string => client.name ?? client.email;
