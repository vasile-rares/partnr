import { redirect } from "next/navigation";
import { requireUser } from "@/lib/auth/session";
import { getPortalWorkspaces } from "@/lib/portal/server";
import { getMyWorkspaces } from "@/lib/workspaces/server";

export default async function HomePage() {
  await requireUser();
  const workspaces = await getMyWorkspaces();

  if (workspaces.length === 0) {
    redirect((await getPortalWorkspaces()).length > 0 ? "/portal" : "/workspaces/new");
  }
  if (workspaces.length === 1) {
    redirect(`/w/${workspaces[0].id}`);
  }
  redirect("/workspaces");
}
