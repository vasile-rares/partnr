"use client";

import { MonitorIcon, MoonIcon, PaletteIcon, SunIcon } from "lucide-react";
import { useTheme } from "next-themes";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

const THEMES = [
  { value: "light", label: "Light", icon: SunIcon },
  { value: "dark", label: "Dark", icon: MoonIcon },
  { value: "system", label: "System", icon: MonitorIcon },
] as const;

/** A "Theme" row for a dropdown menu with the three choices as icons; the menu stays open, ←/→ change the theme from the keyboard. */
export function ThemeMenuItem() {
  const { theme = "system", setTheme } = useTheme();

  function move(step: number) {
    const current = THEMES.findIndex((option) => option.value === theme);
    setTheme(THEMES[(current + step + THEMES.length) % THEMES.length].value);
  }

  return (
    <DropdownMenuItem
      closeOnClick={false}
      className="theme-menu-item h-7 justify-between py-0"
      onKeyDown={(event) => {
        if (event.key === "ArrowRight") move(1);
        if (event.key === "ArrowLeft") move(-1);
      }}
    >
      <PaletteIcon /> Theme
      <Tabs className="theme-menu-item-tabs ml-auto" value={theme} onValueChange={(value) => setTheme(String(value))}>
        <TabsList className="gap-0.5 rounded-full border bg-transparent p-0.5 group-data-horizontal/tabs:h-6">
          {THEMES.map(({ value, label, icon: Icon }) => (
            <TabsTrigger
              key={value}
              value={value}
              aria-label={label}
              className="size-4.5 flex-none rounded-full p-0 data-active:bg-accent dark:data-active:bg-accent [&_svg:not([class*='size-'])]:size-3"
            >
              <Icon />
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>
    </DropdownMenuItem>
  );
}

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
