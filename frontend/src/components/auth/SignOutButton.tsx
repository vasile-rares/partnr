"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { postAuth } from "@/lib/auth/client";

export function SignOutButton() {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  return (
    <button
      type="button"
      disabled={pending}
      onClick={() =>
        startTransition(async () => {
          await postAuth("logout");
          router.replace("/login");
          router.refresh();
        })
      }
      className="h-11 rounded-md border border-line px-4 font-medium outline-offset-2 hover:bg-surface focus-visible:outline-2 focus-visible:outline-done disabled:opacity-60"
    >
      Sign out
    </button>
  );
}
