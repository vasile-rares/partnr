import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { SheetTrigger } from "@/components/ui/sheet";
import { daysUntil, formatDate } from "@/lib/format";
import type { Project } from "@/lib/projects/types";

export function ProjectDeadlineCard({ project }: { project: Project }) {
  const dates = [
    project.startDate ? `Started ${formatDate(project.startDate)}` : null,
    project.dueDate ? `Due ${formatDate(project.dueDate)}` : null,
  ].filter(Boolean);

  return (
    <Card size="sm">
      <CardHeader>
        <CardTitle>Deadline</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-1">
        {project.dueDate ? <Remaining dueDate={project.dueDate} completed={project.status === "Completed"} /> : null}
        {dates.length > 0 ? (
          <p className="text-sm text-muted-foreground">{dates.join(" · ")}</p>
        ) : (
          <p className="text-sm text-muted-foreground">
            No dates yet.{" "}
            {/* Opens the project's edit panel: this card sits inside the page's <Sheet>. */}
            <SheetTrigger render={<Button type="button" variant="link" className="h-auto p-0 underline underline-offset-4" />}>
              Set dates
            </SheetTrigger>
          </p>
        )}
      </CardContent>
    </Card>
  );
}

function Remaining({ dueDate, completed }: { dueDate: string; completed: boolean }) {
  if (completed) {
    return <p className="text-lg font-medium">Completed</p>;
  }

  const days = daysUntil(dueDate);
  if (days < 0) {
    return <p className="text-lg font-medium text-destructive">{days === -1 ? "1 day overdue" : `${-days} days overdue`}</p>;
  }
  if (days === 0) {
    return <p className="text-lg font-medium">Due today</p>;
  }
  return <p className="text-lg font-medium">{days === 1 ? "1 day left" : `${days} days left`}</p>;
}
