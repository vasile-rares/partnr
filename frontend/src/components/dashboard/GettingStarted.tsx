import { AddClientDialog } from "@/components/clients/AddClientDialog";
import { NewProjectDialog } from "@/components/projects/NewProjectDialog";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

type GettingStartedProps = {
  workspaceId: string;
  /** Active clients; none means the first step is still to add one. */
  clients: { id: string; displayName: string }[];
  existingEmails: string[];
};

export function GettingStarted({ workspaceId, clients, existingEmails }: GettingStartedProps) {
  const hasClients = clients.length > 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{hasClients ? "Create a project" : "Add your first client"}</CardTitle>
        <CardDescription>
          {hasClients
            ? "Projects hold the stages, updates, deliverables and files your client follows in their portal."
            : "A client gets an invitation to their own portal, where they follow the projects you do for them."}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {hasClients ? (
          <NewProjectDialog workspaceId={workspaceId} clients={clients} />
        ) : (
          <AddClientDialog workspaceId={workspaceId} existingEmails={existingEmails} />
        )}
      </CardContent>
    </Card>
  );
}
