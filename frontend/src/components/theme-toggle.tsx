"use client";

import { MonitorIcon, MoonIcon, SunIcon } from "lucide-react";
import { useTheme } from "next-themes";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

/** The Light / Dark / System choices, for any dropdown menu. */
export function ThemeRadioGroup() {
  const { theme, setTheme } = useTheme();

  return (
    <DropdownMenuRadioGroup value={theme} onValueChange={(value) => setTheme(value)}>
      <DropdownMenuRadioItem value="light">
        <SunIcon /> Light
      </DropdownMenuRadioItem>
      <DropdownMenuRadioItem value="dark">
        <MoonIcon /> Dark
      </DropdownMenuRadioItem>
      <DropdownMenuRadioItem value="system">
        <MonitorIcon /> System
      </DropdownMenuRadioItem>
    </DropdownMenuRadioGroup>
  );
}

export function ThemeToggle() {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger render={<Button variant="ghost" size="icon" aria-label="Change theme" />}>
        {/* Icons switch through CSS, so server and client render the same markup before the theme is known. */}
        <SunIcon className="dark:hidden" />
        <MoonIcon className="hidden dark:block" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <ThemeRadioGroup />
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
