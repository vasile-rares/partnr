import { randomUUID } from "node:crypto";
import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { PASSWORD, createAccount, mainNav } from "./helpers";
import { invitationTokenIn, waitForEmails } from "./mailpit";

// A studio, a client who joined, and a project the client can see: the conversation exists from then on.
async function studioWithClient(request: APIRequestContext, newClientContext: () => Promise<APIRequestContext>) {
  await createAccount(request);
  const clientEmail = `client-${randomUUID()}@example.com`;
  const workspace = await (await request.post("/api/workspaces", { data: { name: "Studio Pop" } })).json();
  const client = await (
    await request.post(`/api/workspaces/${workspace.id}/clients`, { data: { name: "Elena Popescu", email: clientEmail } })
  ).json();
  expect((await request.post(`/api/workspaces/${workspace.id}/clients/${client.id}/invitation`)).ok()).toBe(true);
  const token = invitationTokenIn((await waitForEmails(clientEmail))[0]);
  const clientRequest = await newClientContext();
  expect((await clientRequest.post(`/api/invitations/${token}/register`, { data: { fullName: "Elena Popescu", password: PASSWORD } })).ok()).toBe(true);

  const project = await (
    await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website" } })
  ).json();
  expect(
    (
      await request.put(`/api/workspaces/${workspace.id}/projects/${project.id}`, {
        data: { name: "Website", description: null, startDate: null, dueDate: null, status: "Active" },
      })
    ).ok(),
  ).toBe(true);

  return { workspace, project, clientRequest };
}

const thread = (page: Page) => page.getByRole("log");
const composer = (page: Page) => page.getByRole("textbox", { name: "Write a message" });

test("the firm and the client talk on the Messages page, and every change reaches the other side live", async ({
  request,
  playwright,
  browser,
  baseURL,
}) => {
  const { workspace, project, clientRequest } = await studioWithClient(request, () => playwright.request.newContext({ baseURL }));
  const studio = await browser.newContext({ baseURL, storageState: await request.storageState() });
  const firm = await studio.newPage();
  const asClient = await browser.newContext({ baseURL, storageState: await clientRequest.storageState() });
  const client = await asClient.newPage();

  // The firm opens Messages from the sidebar and writes first; Shift+Enter is a new line, Enter sends.
  await firm.goto(`/w/${workspace.id}`);
  await mainNav(firm).getByRole("link", { name: "Messages" }).click();
  await expect(firm).toHaveURL(`/w/${workspace.id}/messages`);
  await firm.getByRole("main").getByRole("link", { name: /Website/ }).click();
  await expect(firm).toHaveURL(`/w/${workspace.id}/messages/${project.id}`);
  await expect(firm.getByText("No messages yet. Write the first one below.")).toBeVisible();
  await composer(firm).fill("Hi Elena,");
  await composer(firm).press("Shift+Enter");
  await composer(firm).pressSequentially("the mockups are up.");
  await composer(firm).press("Enter");
  await expect(thread(firm)).toContainText("Hi Elena,\nthe mockups are up.");
  await expect(composer(firm)).toHaveValue("");

  // The client sees one unread message, in the sidebar and in the list; opening the conversation reads it.
  await client.goto(`/portal/${workspace.id}`);
  await expect(mainNav(client).getByLabel("1 unread messages")).toBeVisible();
  await mainNav(client).getByRole("link", { name: "Messages" }).click();
  await expect(client.getByRole("list", { name: "Conversations" }).getByLabel("1 unread")).toBeVisible();
  await client.getByRole("main").getByRole("link", { name: /Website/ }).click();
  await expect(thread(client)).toContainText("Ana Pop");
  await expect(thread(client)).toContainText("the mockups are up.");
  await expect(mainNav(client).getByLabel("1 unread messages")).toHaveCount(0);

  // The client answers; the firm's page, untouched, shows it.
  await composer(client).fill("Thanks, looking now.");
  await composer(client).press("Enter");
  await expect(thread(firm)).toContainText("Thanks, looking now.");
  await expect(thread(firm)).toContainText("Elena Popescu");

  // The firm corrects its message, then deletes it; the client sees both without reloading.
  await firm.getByRole("button", { name: "Edit your message" }).click();
  await firm.getByRole("textbox", { name: "Edit your message" }).fill("Hi Elena, the mockups are ready.");
  await firm.getByRole("button", { name: "Save" }).click();
  await expect(thread(client)).toContainText("Hi Elena, the mockups are ready.");
  await expect(thread(client)).toContainText("edited");

  await firm.getByRole("button", { name: "Delete your message" }).click();
  await firm.getByRole("button", { name: "Delete", exact: true }).click();
  await expect(thread(client)).toContainText("This message was deleted.");
  await expect(thread(client)).not.toContainText("mockups are ready");

  // Someone else's message has no edit or delete.
  await expect(thread(firm).getByRole("button", { name: "Edit your message" })).toHaveCount(0);

  await studio.close();
  await asClient.close();
  await clientRequest.dispose();
});

test("an unknown conversation is not found on either side", async ({ request, playwright, browser, baseURL }) => {
  const { workspace, clientRequest } = await studioWithClient(request, () => playwright.request.newContext({ baseURL }));
  const asClient = await browser.newContext({ baseURL, storageState: await clientRequest.storageState() });
  const client = await asClient.newPage();
  const studio = await browser.newContext({ baseURL, storageState: await request.storageState() });
  const firm = await studio.newPage();

  const unknown = randomUUID();
  expect((await firm.goto(`/w/${workspace.id}/messages/${unknown}`))!.status()).toBe(404);
  expect((await client.goto(`/portal/${workspace.id}/messages/${unknown}`))!.status()).toBe(404);

  await studio.close();
  await asClient.close();
  await clientRequest.dispose();
});
