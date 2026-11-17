import { VideoIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";

/** Opens the meeting's Google Meet call in a new tab. */
export function JoinMeetingButton({ title, url }: { title: string; url: string }) {
  return (
    <a href={url} target="_blank" rel="noopener noreferrer" className={buttonVariants({ size: "sm" })}>
      <VideoIcon data-icon="inline-start" />
      Join
      <span className="sr-only"> {title} on Google Meet</span>
    </a>
  );
}
