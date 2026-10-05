import { SignOutButton } from "@/components/auth/SignOutButton";
import { requireUser } from "@/lib/auth/session";

export default async function HomePage() {
  const user = await requireUser();

  return (
    <main className="mx-auto flex min-h-dvh w-full max-w-3xl flex-col px-6 py-8">
      <header className="flex items-center justify-between">
        <p className="font-display text-xl font-semibold">Client Portal</p>
        <SignOutButton />
      </header>

      <section className="my-auto py-16">
        <h1 className="font-display text-3xl font-semibold">Hi, {user.fullName}</h1>
        <p className="mt-3 max-w-prose text-ink-soft">
          You have no clients or projects yet. They will show up here once you add them.
        </p>
      </section>
    </main>
  );
}
