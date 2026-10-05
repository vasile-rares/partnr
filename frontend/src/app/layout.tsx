import type { Metadata } from "next";
import { Geist } from "next/font/google";
import { cn } from "@/lib/utils";
import { ThemeProvider } from "@/components/theme-provider";
import "./globals.css";

// The font of the shadcn preset; globals.css maps it to Tailwind's font-sans.
const geist = Geist({ subsets: ["latin"], variable: "--font-sans" });

export const metadata: Metadata = {
  title: {
    default: "Client Portal",
    template: "%s | Client Portal",
  },
  description: "Manage your projects and keep your clients informed.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    // next-themes sets the theme class on <html> before React hydrates; this silences the expected mismatch.
    <html lang="en" suppressHydrationWarning className={cn("h-full antialiased", geist.variable)}>
      <body className="min-h-full">
        <ThemeProvider>{children}</ThemeProvider>
      </body>
    </html>
  );
}
