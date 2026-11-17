import { redirect } from "next/navigation";
import { requireUser } from "@/lib/auth/session";
import { getOpenWorkspaces } from "@/lib/workspaces/server";

export default async function HomePage() {
  await requireUser();
  const workspaces = await getOpenWorkspaces();

  if (workspaces.length === 0) {
    redirect("/workspaces/new");
  }
  if (workspaces.length === 1) {
    redirect(`/w/${workspaces[0].id}`);
  }
  redirect("/workspaces");
}
