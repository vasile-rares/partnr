"use client";

import { CalendarIcon } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { formatDate, fromLocalDate, toLocalDate } from "@/lib/format";
import type { DateLimits } from "@/lib/projects/limits";
import { cn } from "@/lib/utils";

type DatePickerProps = {
  id?: string;
  /** "YYYY-MM-DD", or "" for no date. */
  value: string;
  onChange: (value: string) => void;
  /** Days outside these cannot be chosen, and the calendar cannot be paged past them. */
  limits?: DateLimits;
  /** Adds a "Clear" button to the calendar. */
  clearable?: boolean;
  /** Names the form field the chosen date is sent as, for a plain <form>. */
  name?: string;
  placeholder?: string;
  className?: string;
};

/** A date chosen on a calendar, never typed. Days outside `limits` are shown but cannot be selected. */
export function DatePicker({ id, value, onChange, limits, clearable = false, name, placeholder = "Pick a date", className }: DatePickerProps) {
  const [open, setOpen] = useState(false);
  const min = limits?.min ? toLocalDate(limits.min) : undefined;
  const max = limits?.max ? toLocalDate(limits.max) : undefined;
  const selected = value ? toLocalDate(value) : undefined;
  const now = new Date();
  // Opens on the chosen month, or on today's when that is allowed, or on the closest month that is.
  const opensOn = selected ?? (min && now < min ? min : max && now > max ? max : now);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        render={
          <Button id={id} type="button" variant="outline" className={cn("w-full justify-between font-normal", !value && "text-muted-foreground", className)} />
        }
      >
        {value ? formatDate(value) : placeholder}
        <CalendarIcon aria-hidden className="text-muted-foreground" />
      </PopoverTrigger>
      {name ? <input type="hidden" name={name} value={value} /> : null}
      <PopoverContent align="center" className="w-auto p-0">
        <Calendar
          mode="single"
          weekStartsOn={1}
          selected={selected}
          defaultMonth={opensOn}
          startMonth={min}
          endMonth={max}
          disabled={[...(min ? [{ before: min }] : []), ...(max ? [{ after: max }] : [])]}
          onSelect={(date) => {
            if (date) {
              onChange(fromLocalDate(date));
              setOpen(false);
            }
          }}
        />
        {clearable && value ? (
          <div className="border-t p-2">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="w-full"
              onClick={() => {
                onChange("");
                setOpen(false);
              }}
            >
              Clear
            </Button>
          </div>
        ) : null}
      </PopoverContent>
    </Popover>
  );
}
