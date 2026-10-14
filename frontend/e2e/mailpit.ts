import { expect } from "@playwright/test";

// Mailpit (docker-compose.yml) catches every email the API sends. The same instance serves all tests and the dev API,
// so a test only ever looks at the messages sent to its own, unique address.
const MAILPIT = process.env.MAILPIT_URL ?? "http://localhost:8025";

type MailpitMessage = { ID: string; Subject: string; Created: string };

export type ReceivedEmail = { subject: string; text: string };

async function messagesTo(address: string): Promise<MailpitMessage[]> {
  const response = await fetch(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:${address}`)}`);
  expect(response.ok, "Mailpit did not answer (is `docker compose up -d` running?)").toBe(true);

  return ((await response.json()) as { messages: MailpitMessage[] }).messages;
}

/** Waits until `count` emails arrived for the address and returns them, oldest first. */
export async function waitForEmails(address: string, count = 1): Promise<ReceivedEmail[]> {
  let found: MailpitMessage[] = [];
  await expect
    .poll(async () => (found = await messagesTo(address)).length, { message: `emails sent to ${address}`, timeout: 15_000 })
    .toBeGreaterThanOrEqual(count);

  const oldestFirst = [...found].sort((a, b) => a.Created.localeCompare(b.Created));

  return Promise.all(
    oldestFirst.map(async ({ ID, Subject }) => {
      const detail = await fetch(`${MAILPIT}/api/v1/message/${ID}`);
      return { subject: Subject, text: ((await detail.json()) as { Text: string }).Text };
    }),
  );
}

/** The token from the invitation link in an email body. */
export function invitationTokenIn(email: ReceivedEmail): string {
  const token = email.text.match(/\/invite\/([A-Za-z0-9_-]+)/)?.[1];
  expect(token, "an invitation link in the email").toBeTruthy();

  return token!;
}
