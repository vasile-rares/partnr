import type { Metadata, Viewport } from "next";
import { Geist } from "next/font/google";
import { cn } from "@/lib/utils";
import { APP_NAME } from "@/lib/brand";
import { ThemeColor } from "@/components/theme-color";
import { ThemeProvider } from "@/components/theme-provider";
import { Toaster } from "@/components/ui/sonner";
import "./globals.css";

// The font of the shadcn preset; globals.css maps it to Tailwind's font-sans.
const geist = Geist({ subsets: ["latin"], variable: "--font-sans" });

export const metadata: Metadata = {
  title: {
    default: APP_NAME,
    template: `%s | ${APP_NAME}`,
  },
  description: "Manage your projects and keep your clients informed.",
};

// The dark value is the default theme's; `ThemeColor` changes it when the person picks Light.
export const viewport: Viewport = {
  themeColor: "#181818",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    // next-themes sets the theme class on <html> before React hydrates; this silences the expected mismatch.
    <html lang="en" suppressHydrationWarning className={cn("h-full antialiased", geist.variable)}>
      <body className="app-body min-h-full">
        <ThemeProvider>
          {children}
          <ThemeColor />
          <Toaster position="bottom-right" />
        </ThemeProvider>
      </body>
    </html>
  );
}
