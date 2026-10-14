import { redirect } from "next/navigation";
import { requireUser } from "@/lib/auth/session";
import { getPortalWorkspaces } from "@/lib/portal/server";
import { getMyWorkspaces } from "@/lib/workspaces/server";

// "/" has no page of its own: it sends you to where you most likely want to be.
export default async function HomePage() {
  await requireUser();
  const workspaces = await getMyWorkspaces();

  if (workspaces.length === 0) {
    // Someone who only is a client has no workspace to create: they go to their portal.
    redirect((await getPortalWorkspaces()).length > 0 ? "/portal" : "/workspaces/new");
  }
  if (workspaces.length === 1) {
    redirect(`/w/${workspaces[0].id}`);
  }
  redirect("/workspaces");
}
