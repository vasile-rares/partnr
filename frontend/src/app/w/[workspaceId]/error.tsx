"use client";

import { ErrorState } from "@/components/layout/ErrorState";
import { Page } from "@/components/shell/Page";

// Inside the workspace layout, so the sidebar stays and the person can move on to another page.
export default function Error({ error, retry }: { error: Error & { digest?: string }; retry: () => void }) {
  return (
    <Page crumbs={[{ label: "Error" }]}>
      <ErrorState error={error} retry={retry} />
    </Page>
  );
}
