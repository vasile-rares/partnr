"use client";

import { useState, useTransition, type ComponentProps, type ReactNode } from "react";
import { FormError } from "@/components/forms/FormMessage";
import { Button } from "@/components/ui/button";
import { sendJson } from "@/lib/api/client";

type ConnectGoogleButtonProps = Pick<ComponentProps<typeof Button>, "variant" | "size"> & {
  children: ReactNode;
  /** A path of the app to come back to after Google ("/w/{id}/calendar"); the account page when left out. */
  returnTo?: string;
};

/** Starts the Google connection from wherever it is needed: asks the API for Google's consent page and goes straight there. */
export function ConnectGoogleButton({ variant, size, children, returnTo }: ConnectGoogleButtonProps) {
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState<string>();

  // Google's own page comes next, so this leaves the app instead of refreshing it.
  function connect() {
    startTransition(async () => {
      setMessage(undefined);
      const result = await sendJson<{ url: string }>("POST", "/api/integrations/google/connect", returnTo ? { returnTo } : undefined);
      if (result.ok) {
        window.location.assign(result.data.url);
      } else {
        setMessage(result.message);
      }
    });
  }

  return (
    <div className="connect-google flex flex-col items-start gap-2">
      <Button type="button" variant={variant} size={size} onClick={connect} disabled={pending}>
        {children}
      </Button>
      <FormError message={message} />
    </div>
  );
}
