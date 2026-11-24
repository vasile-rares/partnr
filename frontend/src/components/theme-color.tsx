"use client";

import { useTheme } from "next-themes";
import { useEffect } from "react";

// The browser bar on phones takes this colour; it has to follow the --background of the theme in use (globals.css).
const THEME_COLORS = { dark: "#181818", light: "#ffffff" };

export function ThemeColor() {
  const { resolvedTheme } = useTheme();

  useEffect(() => {
    const color = resolvedTheme === "light" ? THEME_COLORS.light : THEME_COLORS.dark;
    document.querySelector('meta[name="theme-color"]')?.setAttribute("content", color);
  }, [resolvedTheme]);

  return null;
}
