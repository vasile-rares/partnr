import type { Metadata } from "next";
import Link from "next/link";
import { ChevronRightIcon } from "lucide-react";
import { AppHeader } from "@/components/AppHeader";
import { buttonVariants } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { requireUser } from "@/lib/auth/session";
import { getMyWorkspaces } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Your workspaces" };

export default async function WorkspacesPage() {
  await requireUser();
  const workspaces = await getMyWorkspaces();

  return (
    <>
      <AppHeader />
      <main className="mx-auto w-full max-w-3xl px-6 py-10">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <h1 className="font-heading text-3xl font-semibold">Your workspaces</h1>
          {/* A link styled as a button: Base UI's Button would replace the link role (see shadcn's Button docs). */}
          <Link href="/workspaces/new" className={buttonVariants({ size: "lg" })}>
            Create workspace
          </Link>
        </div>

        {workspaces.length === 0 ? (
          <p className="mt-6 max-w-prose text-muted-foreground">
            You are not in any workspace yet. Create one to start adding clients and projects.
          </p>
        ) : (
          <Card className="mt-8 gap-0 py-0">
            <ul className="divide-y">
              {workspaces.map((workspace) => (
                <li key={workspace.id}>
                  <Link
                    href={`/w/${workspace.id}`}
                    className="flex items-center justify-between gap-4 px-4 py-3 outline-none hover:bg-muted/50 focus-visible:bg-muted/50"
                  >
                    <span className="truncate font-medium">{workspace.name}</span>
                    <span className="flex shrink-0 items-center gap-2 text-sm text-muted-foreground">
                      {workspace.role}
                      <ChevronRightIcon className="size-4" aria-hidden />
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </Card>
        )}
      </main>
    </>
  );
}
