import Link from "next/link";

export default function NotFound() {
  return (
    <main className="mx-auto flex min-h-dvh w-full max-w-3xl flex-col justify-center px-6 py-10">
      <h1 className="font-heading text-3xl font-semibold">Page not found</h1>
      <p className="mt-3 max-w-prose text-muted-foreground">
        This page does not exist, or it belongs to a workspace you are not part of.
      </p>
      <Link href="/" className="mt-6 font-medium text-foreground underline underline-offset-4">
        Go to your workspaces
      </Link>
    </main>
  );
}
