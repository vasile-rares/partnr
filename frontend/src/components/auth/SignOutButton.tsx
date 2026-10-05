"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { Button } from "@/components/ui/button";
import { postAuth } from "@/lib/auth/client";

export function SignOutButton() {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  return (
    <Button
      variant="outline"
      disabled={pending}
      onClick={() =>
        startTransition(async () => {
          await postAuth("logout");
          router.replace("/login");
          router.refresh();
        })
      }
    >
      Sign out
    </Button>
  );
}
