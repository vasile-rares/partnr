"use client";

import Link from "next/link";
import { useEffect } from "react";
import { Button, buttonVariants } from "@/components/ui/button";

type ErrorStateProps = {
  error: Error & { digest?: string };
  retry: () => void;
  /** Where "Go back" leads when trying again does not help. */
  homeHref?: string;
};

/** The screen an `error.tsx` shows when a page fails to render (the API is down, an unexpected response). */
export function ErrorState({ error, retry, homeHref = "/" }: ErrorStateProps) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div role="alert" className="error-state mx-auto flex w-full max-w-3xl flex-col items-start gap-4 px-6 py-16">
      <h1 className="font-heading text-2xl font-semibold text-balance">Something went wrong</h1>
      <p className="max-w-prose text-muted-foreground">
        We could not load this page. Check your connection and try again; if it keeps happening, come back in a few minutes.
      </p>
      {error.digest ? <p className="text-xs text-muted-foreground">Reference: {error.digest}</p> : null}
      <div className="error-state-actions flex gap-2">
        <Button onClick={() => retry()}>Try again</Button>
        <Link href={homeHref} className={buttonVariants({ variant: "outline" })}>
          Go to your workspaces
        </Link>
      </div>
    </div>
  );
}
