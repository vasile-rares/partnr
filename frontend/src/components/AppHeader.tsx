import Link from "next/link";
import { SignOutButton } from "@/components/auth/SignOutButton";
import { ThemeToggle } from "@/components/theme-toggle";
import { buttonVariants } from "@/components/ui/button";

type AppHeaderProps = {
  /** The workspace being viewed, if any: its name is shown and its settings are linked. */
  workspace?: { id: string; name: string };
};

export function AppHeader({ workspace }: AppHeaderProps) {
  return (
    <header className="border-b bg-card">
      <div className="mx-auto flex w-full max-w-3xl flex-wrap items-center justify-between gap-x-6 gap-y-3 px-6 py-3">
        <div className="flex min-w-0 items-baseline gap-2">
          <Link href="/" className="shrink-0 font-heading text-lg font-semibold">
            Client Portal
          </Link>
          {workspace ? (
            <>
              <span aria-hidden className="text-muted-foreground">
                /
              </span>
              <Link href={`/w/${workspace.id}`} className="truncate font-medium">
                {workspace.name}
              </Link>
            </>
          ) : null}
        </div>

        <nav aria-label="Account" className="flex items-center gap-1">
          {workspace ? (
            <>
              <Link href={`/w/${workspace.id}/clients`} className={buttonVariants({ variant: "ghost" })}>
                Clients
              </Link>
              <Link href={`/w/${workspace.id}/projects`} className={buttonVariants({ variant: "ghost" })}>
                Projects
              </Link>
              <Link href={`/w/${workspace.id}/settings`} className={buttonVariants({ variant: "ghost" })}>
                Settings
              </Link>
            </>
          ) : null}
          <Link href="/workspaces" className={buttonVariants({ variant: "ghost" })}>
            All workspaces
          </Link>
          <Link href="/account" className={buttonVariants({ variant: "ghost" })}>
            Account
          </Link>
          <ThemeToggle />
          <SignOutButton />
        </nav>
      </div>
    </header>
  );
}
