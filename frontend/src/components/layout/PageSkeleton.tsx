import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { SidebarTrigger } from "@/components/ui/sidebar";
import { cn } from "@/lib/utils";

/** What a page looks like while its data loads (`loading.tsx`): the same top bar and column as `Page`, with grey blocks for the content. */
export function PageSkeleton({ narrow = false }: { narrow?: boolean }) {
  return (
    <div role="status" className="page-skeleton">
      <span className="sr-only">Loading…</span>
      <header className="page-skeleton-topbar flex min-h-14 shrink-0 items-center gap-2 border-b px-4 py-2" aria-hidden>
        <SidebarTrigger className="-ml-1" />
        <Separator orientation="vertical" className="mr-1 data-vertical:h-4 data-vertical:self-center" />
        <Skeleton className="h-4 w-40" />
      </header>
      <div className={cn("page-skeleton-content mx-auto flex w-full flex-col gap-8 px-6 py-8", narrow ? "max-w-3xl" : "max-w-5xl")} aria-hidden>
        <div className="page-skeleton-heading flex flex-col gap-3">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-96 max-w-full" />
        </div>
        <div className="page-skeleton-blocks flex flex-col gap-3">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      </div>
    </div>
  );
}
