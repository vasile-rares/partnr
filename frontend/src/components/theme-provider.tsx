"use client";

import { ThemeProvider as NextThemesProvider } from "next-themes";
import type { ComponentProps } from "react";

// next-themes puts the `dark` class on <html> (what globals.css keys dark mode on) and remembers the choice.
export function ThemeProvider(props: ComponentProps<typeof NextThemesProvider>) {
  return <NextThemesProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange {...props} />;
}
