"use client";

import { ErrorState } from "@/components/layout/ErrorState";
import { Page } from "@/components/shell/Page";

export default function Error({ error, retry }: { error: Error & { digest?: string }; retry: () => void }) {
  return (
    <Page crumbs={[{ label: "Error" }]}>
      <ErrorState error={error} retry={retry} />
    </Page>
  );
}
