/** A client as the API returns it. Optional details are null when not set. */
export type Client = {
  id: string;
  name: string;
  email: string;
  company: string | null;
  phone: string | null;
  notes: string | null;
  createdAtUtc: string;
  /** Set when the client is archived (hidden from the default list). */
  archivedAtUtc: string | null;
};
