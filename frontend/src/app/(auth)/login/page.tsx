import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { LoginForm } from "@/components/auth/LoginForm";
import { safeNextPath } from "@/lib/auth/redirect";
import { getCurrentUser } from "@/lib/auth/session";

export const metadata: Metadata = { title: "Sign in" };

export default async function LoginPage({ searchParams }: PageProps<"/login">) {
  const next = safeNextPath((await searchParams).next);

  if (await getCurrentUser()) {
    redirect(next);
  }

  return (
    <>
      <h1 className="mb-8 font-display text-3xl font-semibold">Sign in</h1>
      <LoginForm next={next} />
    </>
  );
}
