import type { ReactNode } from "react";
import { ProjectTimeline } from "@/components/auth/ProjectTimeline";
import { ThemeToggle } from "@/components/theme-toggle";

export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="grid min-h-dvh lg:grid-cols-[5fr_6fr]">
      <main className="flex flex-col px-6 py-8 sm:px-12 lg:px-16">
        <div className="flex items-center justify-between">
          <p className="font-heading text-xl font-semibold">Client Portal</p>
          <ThemeToggle />
        </div>
        <div className="my-auto w-full max-w-sm py-12">{children}</div>
      </main>

      <aside className="hidden items-center border-l bg-muted/40 px-16 lg:flex xl:px-24">
        <ProjectTimeline />
      </aside>
    </div>
  );
}
