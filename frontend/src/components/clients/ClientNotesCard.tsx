import { ShieldIcon } from "lucide-react";
import { InfoTip } from "@/components/actions/InfoTip";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { SheetTrigger } from "@/components/ui/sheet";

/** The private notes, so they show without opening the edit panel; its trigger sits inside the page's `Sheet`. */
export function ClientNotesCard({ notes }: { notes: string | null }) {
  return (
    <section aria-labelledby="notes-title">
      <Card size="sm">
        <CardHeader>
          <CardTitle id="notes-title" className="flex items-center gap-1">
            Notes
            <InfoTip label="Who can see the notes" icon={<ShieldIcon className="size-3.5" />} text="Only visible to your team">
              Clients never see these notes.
            </InfoTip>
          </CardTitle>
        </CardHeader>
        <CardContent>
          {notes ? (
            <p className="whitespace-pre-line break-words text-sm">{notes}</p>
          ) : (
            <p className="text-sm text-muted-foreground">
              No notes yet.{" "}
              <SheetTrigger render={<Button type="button" variant="link" className="h-auto p-0 underline underline-offset-4" />}>
                Add notes
              </SheetTrigger>
            </p>
          )}
        </CardContent>
      </Card>
    </section>
  );
}
