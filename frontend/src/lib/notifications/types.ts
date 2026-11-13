// What the API answers for the signed-in account's own inbox (GET /api/notifications).

export type NotificationItem = {
  id: string;
  /** One short line, the same as the email's subject. */
  title: string;
  /** The sentence under it. */
  text: string;
  /** Where it leads, inside this app (starts with a slash). */
  path: string;
  workspaceName: string;
  createdAtUtc: string;
  isRead: boolean;
};

export type Notifications = {
  /** Every unread one, not only those in `items`. */
  unread: number;
  items: NotificationItem[];
};
