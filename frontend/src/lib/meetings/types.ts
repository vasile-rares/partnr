export type MeetingStatus = "Scheduled" | "Cancelled";

/** Where the meeting's Google event stands. Only the freelancer's side ever sees it. */
export type MeetingSyncStatus = "Pending" | "Synced" | "Failed" | "NeedsReconnect";

export type Meeting = {
  id: string;
  client: { id: string; displayName: string };
  project: { id: string; name: string } | null;
  title: string;
  description: string | null;
  /** A moment, always UTC: it is shown in the reader's own time zone. */
  startsAtUtc: string;
  endsAtUtc: string;
  status: MeetingStatus;
  createdAtUtc: string;
  cancelledAtUtc: string | null;
  syncStatus: MeetingSyncStatus;
  /** Null until Google has made the event. */
  meetUrl: string | null;
};

/** What the client sees: no client card, no organiser, nothing about the sync. */
export type PortalMeeting = {
  id: string;
  /** Only a project the client can open is named. */
  project: { id: string; name: string } | null;
  title: string;
  description: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  status: MeetingStatus;
  /** Null for a cancelled meeting, and until Google has made the event. */
  meetUrl: string | null;
};

/** What both kinds of meeting share, which is all the time helpers need. */
export type Timed = Pick<Meeting, "startsAtUtc" | "endsAtUtc" | "status">;
