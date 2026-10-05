import { CircleAlertIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";

/** The form-level error under the fields (e.g. wrong password). Alert has role="alert", so it is announced. */
export function FormError({ message }: { message?: string }) {
  if (!message) {
    return null;
  }

  return (
    <Alert variant="destructive">
      <CircleAlertIcon />
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  );
}
