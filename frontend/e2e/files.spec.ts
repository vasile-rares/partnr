import { randomUUID } from "node:crypto";
import { readFileSync } from "node:fs";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { alertWith, createAccount, createWorkspace, signIn } from "./helpers";
import { invitationTokenIn, waitForEmails } from "./mailpit";

const text = (name: string, content: string) => ({ name, mimeType: "text/plain", buffer: Buffer.from(content) });

test("files are uploaded, downloaded, shared, edited, archived and deleted", async ({ page, request }) => {
  const user = await createAccount(request);
  const workspace = await createWorkspace(request, "Studio");
  const client = await (
    await request.post(`/api/workspaces/${workspace.id}/clients`, { data: { name: "Acme SRL", email: `client-${randomUUID()}@example.com` } })
  ).json();
  const project = await (await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website" } })).json();
  await page.goto("/login");
  await signIn(page, user.email, user.password);
  await expect(page).toHaveURL(`/w/${workspace.id}`);

  await page.goto(`/w/${workspace.id}/projects/${project.id}`);
  const files = page.getByRole("region", { name: "Files" });
  await expect(files.getByText("No files yet.")).toBeVisible();

  await files.getByLabel("File", { exact: true }).setInputFiles(text("brief.txt", "Read me first."));
  await files.getByLabel("Display name").fill("Project brief");
  await files.getByLabel("Short note").fill("Goals and pages.");
  await files.getByRole("button", { name: "Upload file" }).click();
  const item = files.getByRole("listitem").filter({ hasText: "Project brief" });
  await expect(item).toContainText("Draft");
  await expect(item).toContainText("Goals and pages.");
  await expect(item).toContainText("brief.txt · 14 B");
  await expect(files.getByLabel("Display name")).toHaveValue("");

  // A file type that is not allowed is refused by the API, and its message reaches the form.
  await files.getByLabel("File", { exact: true }).setInputFiles({ name: "setup.exe", mimeType: "application/octet-stream", buffer: Buffer.from("MZ") });
  await files.getByRole("button", { name: "Upload file" }).click();
  await expect(alertWith(page, "Files of this type are not allowed.")).toBeVisible();

  // A file over 25 MB is refused before it is sent.
  await files.getByLabel("File", { exact: true }).setInputFiles({
    name: "huge.zip",
    mimeType: "application/zip",
    buffer: Buffer.alloc(25 * 1024 * 1024 + 1),
  });
  await files.getByRole("button", { name: "Upload file" }).click();
  await expect(alertWith(page, "huge.zip is larger than 25 MB.")).toBeVisible();
  await expect(files.getByRole("listitem")).toHaveCount(1);

  const downloading = page.waitForEvent("download");
  await item.getByRole("link", { name: "Download Project brief" }).click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe("brief.txt");
  expect(readFileSync(await download.path(), "utf8")).toBe("Read me first.");

  // A shared file cannot be deleted; it goes back to draft first.
  await item.getByRole("button", { name: "Share Project brief" }).click();
  await expect(item).toContainText("Shared");
  await expect(item.getByRole("button", { name: "Delete Project brief" })).toBeHidden();
  await item.getByRole("button", { name: "Unshare Project brief" }).click();
  await expect(item).toContainText("Draft");

  await item.getByRole("button", { name: "Edit Project brief" }).click();
  await files.getByLabel("Name", { exact: true }).fill("Kickoff brief");
  await files.getByLabel("Description", { exact: true }).fill("");
  await files.getByRole("button", { name: "Save file" }).click();
  const renamed = files.getByRole("listitem").filter({ hasText: "Kickoff brief" });
  await expect(renamed).toBeVisible();
  await expect(renamed).not.toContainText("Goals and pages.");
  await expect(renamed).toContainText("brief.txt"); // the file itself is not renamed

  await renamed.getByRole("button", { name: "Archive Kickoff brief" }).click();
  await expect(files.getByRole("heading", { name: "Archived" })).toBeVisible();
  const archived = files.getByRole("listitem").filter({ hasText: "Kickoff brief" });
  await expect(archived).toContainText("Archived");
  await expect(archived.getByRole("button", { name: "Edit Kickoff brief" })).toBeHidden();
  await expect(archived.getByRole("link", { name: "Download Kickoff brief" })).toBeVisible();
  await archived.getByRole("button", { name: "Restore Kickoff brief" }).click();
  await expect(files.getByRole("heading", { name: "Archived" })).toBeHidden();
  await expect(renamed).toContainText("Draft");

  await renamed.getByRole("button", { name: "Delete Kickoff brief" }).click();
  await page.getByRole("button", { name: "Keep file" }).click();
  await expect(renamed).toBeVisible();
  await renamed.getByRole("button", { name: "Delete Kickoff brief" }).click();
  await page.getByRole("button", { name: "Delete file" }).click();
  await expect(files.getByText("No files yet.")).toBeVisible();

  await files.getByLabel("File", { exact: true }).setInputFiles(text("notes.txt", "Later."));
  await files.getByRole("button", { name: "Upload file" }).click();
  await expect(files.getByRole("listitem").filter({ hasText: "notes.txt" })).toBeVisible();
  await page.getByRole("button", { name: "Archive project" }).click();
  await expect(page.getByText("This project is archived")).toBeVisible();
  await expect(files.getByRole("button", { name: "Upload file" })).toBeHidden();
  await expect(files.getByRole("button", { name: "Share notes.txt" })).toBeHidden();
  await expect(files.getByRole("link", { name: "Download notes.txt" })).toBeVisible();
});

async function uploadTo(request: APIRequestContext, url: string, fileName: string, content: string, name: string, share: boolean) {
  const response = await request.post(url, { multipart: { file: text(fileName, content), name } });
  expect(response.status()).toBe(201);
  const file = await response.json();
  if (share) {
    expect((await request.post(`${url}/${file.id}/share`)).ok()).toBe(true);
  }
  return file;
}

test("a client sees and downloads only the files the freelancer shared", async ({ page, request }) => {
  await createAccount(request);
  const clientEmail = `client-${randomUUID()}@example.com`;
  const workspace = await createWorkspace(request, "Studio Pop");
  const client = await (
    await request.post(`/api/workspaces/${workspace.id}/clients`, { data: { name: "Elena Ionescu", email: clientEmail } })
  ).json();
  expect((await request.post(`/api/workspaces/${workspace.id}/clients/${client.id}/invitation`)).ok()).toBe(true);
  const [email] = await waitForEmails(clientEmail);
  const project = await (await request.post(`/api/workspaces/${workspace.id}/projects`, { data: { clientId: client.id, name: "Website" } })).json();
  const projectUrl = `/api/workspaces/${workspace.id}/projects/${project.id}`;
  expect(
    (await request.put(projectUrl, { data: { name: "Website", description: null, startDate: null, dueDate: null, status: "Active" } })).ok(),
  ).toBe(true);
  const brief = await uploadTo(request, `${projectUrl}/files`, "brief.txt", "Read me first.", "Project brief", true);
  await uploadTo(request, `${projectUrl}/files`, "secret.txt", "Not for the client.", "Secret draft", false);

  await page.goto(`/invite/${invitationTokenIn(email)}`);
  await page.getByLabel("Full name").fill("Elena Popescu");
  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByRole("button", { name: "Create account and accept" }).click();
  await expect(page).toHaveURL("/portal");

  await page.goto(`/portal/projects/${project.id}`);
  const files = page.getByRole("region", { name: "Files" });
  await expect(files.getByRole("listitem")).toHaveCount(1);
  await expect(files.getByRole("listitem")).toContainText("Project brief");
  await expect(files.getByRole("listitem")).toContainText("brief.txt · 14 B");
  await expect(page.getByText("Secret draft")).toHaveCount(0);

  const downloading = page.waitForEvent("download");
  await files.getByRole("link", { name: "Download Project brief" }).click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe("brief.txt");
  expect(readFileSync(await download.path(), "utf8")).toBe("Read me first.");

  // Taking the file back hides it from the page, and its link stops working.
  expect((await request.post(`${projectUrl}/files/${brief.id}/unshare`)).ok()).toBe(true);
  await page.reload();
  await expect(page.getByRole("region", { name: "Files" })).toHaveCount(0);
  const gone = await page.request.get(`/api/portal/projects/${project.id}/files/${brief.id}/download`);
  expect(gone.status()).toBe(404);
});
