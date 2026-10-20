import Link from "next/link";
import { Fragment, type ReactNode } from "react";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { Separator } from "@/components/ui/separator";
import { SidebarTrigger } from "@/components/ui/sidebar";
import { cn } from "@/lib/utils";

export type Crumb = { label: string; href?: string };

type PageFrameProps = {
  /** Where this page sits; the last one is the page itself. */
  crumbs: Crumb[];
  /** Forms and other single-column pages. */
  narrow?: boolean;
  children: ReactNode;
};

/** The top bar (sidebar toggle and breadcrumb) and the content column of a page inside the app shell. */
export function Page({ crumbs, narrow = false, children }: PageFrameProps) {
  return (
    <>
      <header className="sticky top-0 z-10 flex min-h-14 shrink-0 items-center gap-2 border-b bg-background px-4 py-2">
        <SidebarTrigger className="-ml-1" />
        <Separator orientation="vertical" className="mr-1 data-vertical:h-4 data-vertical:self-center" />
        <Breadcrumb>
          <BreadcrumbList>
            {crumbs.map((crumb, index) => (
              <Fragment key={crumb.label}>
                {index > 0 ? <BreadcrumbSeparator /> : null}
                <BreadcrumbItem>
                  {index === crumbs.length - 1 || !crumb.href ? (
                    <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
                  ) : (
                    <BreadcrumbLink render={<Link href={crumb.href} />}>{crumb.label}</BreadcrumbLink>
                  )}
                </BreadcrumbItem>
              </Fragment>
            ))}
          </BreadcrumbList>
        </Breadcrumb>
      </header>
      <div className={cn("mx-auto w-full px-6 py-8", narrow ? "max-w-3xl" : "max-w-5xl")}>{children}</div>
    </>
  );
}
