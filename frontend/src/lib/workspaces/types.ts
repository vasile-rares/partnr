/** A workspace as the API returns it; `role` is the signed-in user's role in it. */
export type Workspace = {
  id: string;
  name: string;
  role: "Owner" | "Member";
  createdAtUtc: string;
};

/** A workspace opened by someone who is a client of it: they only follow their own projects there. */
export type ClientWorkspace = Omit<Workspace, "role"> & { role: "Client" };

/** A workspace the person can open, as a member or as a client; `role` tells which. */
export type OpenWorkspace = Workspace | ClientWorkspace;
