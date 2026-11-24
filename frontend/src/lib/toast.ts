import { toast } from "sonner";

// The last segment of a row action's path says what happened; anything else is read from the method.
const doneByAction: Record<string, string> = {
  share: "shared with the client",
  unshare: "hidden from the client",
  archive: "archived",
  restore: "restored",
  cancel: "cancelled",
  withdraw: "request withdrawn",
  approvals: "sent for approval",
  "retry-sync": "sent to Google again",
};

/** "Update archived": confirms a row action of the project lists (`noun` is "Update", "File", ...). `path` is the one sent to `run`. */
export function toastDone(noun: string, method: "POST" | "PUT" | "DELETE", path: string) {
  const action = path.split("/").pop() ?? "";
  const done = doneByAction[action] ?? (method === "DELETE" ? "deleted" : method === "PUT" ? "saved" : "added");
  toast.success(`${noun} ${done}`);
}
