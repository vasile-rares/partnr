import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { AppHeader } from "@/components/AppHeader";
import { buttonVariants } from "@/components/ui/button";
import { requireUser } from "@/lib/auth/session";
import { getWorkspace } from "@/lib/workspaces/server";

export async function generateMetadata({ params }: PageProps<"/w/[workspaceId]">): Promise<Metadata> {
  const workspace = await getWorkspace((await params).workspaceId);
  return { title: workspace?.name ?? "Not found" };
}

export default async function WorkspacePage({ params }: PageProps<"/w/[workspaceId]">) {
  await requireUser();
  // Not a member and does not exist look the same, on purpose (the API answers 404 for both).
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }

  return (
    <>
      <AppHeader workspace={workspace} />
      <main className="mx-auto w-full max-w-3xl px-6 py-10">
        <h1 className="font-heading text-3xl font-semibold">{workspace.name}</h1>

        <section aria-labelledby="clients-title" className="mt-10">
          <h2 id="clients-title" className="font-heading text-xl font-semibold">
            Clients
          </h2>
          <p className="mt-2 max-w-prose text-muted-foreground">
            The people you work for. Each client will get their own projects and portal.
          </p>
          <Link href={`/w/${workspace.id}/clients`} className={`${buttonVariants()} mt-4`}>
            Manage clients
          </Link>
        </section>
      </main>
    </>
  );
}
