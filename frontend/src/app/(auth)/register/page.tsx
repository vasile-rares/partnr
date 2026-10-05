import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { RegisterForm } from "@/components/auth/RegisterForm";
import { getCurrentUser } from "@/lib/auth/session";

export const metadata: Metadata = { title: "Create account" };

export default async function RegisterPage() {
  if (await getCurrentUser()) {
    redirect("/");
  }

  return (
    <>
      <h1 className="mb-8 font-display text-3xl font-semibold">Create account</h1>
      <RegisterForm />
    </>
  );
}
