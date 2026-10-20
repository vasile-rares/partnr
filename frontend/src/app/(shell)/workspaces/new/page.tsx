import type { Metadata } from "next";
import { Page } from "@/components/shell/Page";
import { CreateWorkspaceForm } from "@/components/workspaces/CreateWorkspaceForm";
import { requireUser } from "@/lib/auth/session";
import { getMyWorkspaces } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Create a workspace" };

export default async function NewWorkspacePage() {
  await requireUser();
  const isFirst = (await getMyWorkspaces()).length === 0;

  return (
    <Page crumbs={[{ label: "Workspaces", href: "/workspaces" }, { label: "Create" }]} narrow>
      <h1 className="font-heading text-3xl font-semibold">
        {isFirst ? "Create your workspace" : "Create a workspace"}
      </h1>
      <p className="mt-3 mb-8 max-w-prose text-muted-foreground">
        A workspace holds your clients and their projects. Freelancers usually need one; an agency can keep one per team or brand.
      </p>
      <CreateWorkspaceForm />
    </Page>
  );
}
