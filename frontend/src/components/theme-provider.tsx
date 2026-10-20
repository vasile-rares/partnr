"use client";

import { ThemeProvider as NextThemesProvider } from "next-themes";
import type { ComponentProps } from "react";

// next-themes puts the `dark` class on <html> (what globals.css keys dark mode on) and remembers the choice.
// Dark is the default until someone picks Light or System.
export function ThemeProvider(props: ComponentProps<typeof NextThemesProvider>) {
  return <NextThemesProvider attribute="class" defaultTheme="dark" enableSystem disableTransitionOnChange {...props} />;
}
