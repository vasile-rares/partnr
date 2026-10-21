import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { initials } from "@/lib/format";
import { cn } from "@/lib/utils";

export function ClientAvatar({ name, className }: { name: string; className?: string }) {
  return (
    <Avatar className={cn("size-9", className)}>
      <AvatarFallback>{initials(name)}</AvatarFallback>
    </Avatar>
  );
}
