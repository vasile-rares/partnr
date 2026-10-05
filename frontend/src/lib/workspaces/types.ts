/** A workspace as the API returns it; `role` is the signed-in user's role in it. */
export type Workspace = {
  id: string;
  name: string;
  role: "Owner" | "Member";
  createdAtUtc: string;
};
