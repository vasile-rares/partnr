export type MemberRole = "Owner" | "Member";

export type Member = {
  userId: string;
  /** The account's name, or its email when it has none. */
  name: string;
  email: string;
  role: MemberRole;
  joinedAtUtc: string;
};

/** An invitation that is still open: `Expired` ones stay listed until they are cancelled or sent again. */
export type MemberInvitation = {
  id: string;
  email: string;
  status: "Pending" | "Expired";
  createdAtUtc: string;
  expiresAtUtc: string;
};
