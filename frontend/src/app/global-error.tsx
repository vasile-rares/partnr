"use client";

import "./globals.css";

// Replaces the root layout, so it brings its own <html> and <body> and cannot use the theme provider.
export default function GlobalError({ retry }: { error: Error & { digest?: string }; retry: () => void }) {
  return (
    <html lang="en" className="dark h-full antialiased">
      <body className="app-body min-h-full">
        <main className="global-error mx-auto flex min-h-dvh w-full max-w-3xl flex-col justify-center gap-4 px-6 py-10">
          <h1 className="text-3xl font-semibold text-balance">Something went wrong</h1>
          <p className="max-w-prose text-muted-foreground">The app could not start. Try again; if it keeps happening, come back in a few minutes.</p>
          <button
            type="button"
            onClick={() => retry()}
            className="w-fit rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/80 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            Try again
          </button>
        </main>
      </body>
    </html>
  );
}
