"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { Button } from "@/components/ui/button";
import { postAuth } from "@/lib/auth/client";

/** Signs out and stays on the same page, so the invitation then offers the sign-in or sign-up that fits the invited address. */
export function SwitchAccountButton() {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  return (
    <Button
      variant="outline"
      size="lg"
      className="w-full"
      disabled={pending}
      onClick={() =>
        startTransition(async () => {
          await postAuth("logout");
          router.refresh();
        })
      }
    >
      Sign out and continue
    </Button>
  );
}
