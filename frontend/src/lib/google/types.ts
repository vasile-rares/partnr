/** The signed-in account's own link to Google Calendar. `available` is false when the server has no Google credentials. */
export type GoogleConnection = {
  available: boolean;
  status: "NotConnected" | "Connected" | "NeedsReconnect";
  email: string | null;
};

/** What Google's round trip ended with, as the API puts it in `?google=` when it sends the browser back to /account. */
export type GoogleResult = "connected" | "denied" | "permission" | "error";

export const googleResults: GoogleResult[] = ["connected", "denied", "permission", "error"];

/** The value comes straight from the URL, so anything else is ignored. */
export const parseGoogleResult = (value: string | string[] | undefined): GoogleResult | null =>
  googleResults.find((result) => result === value) ?? null;
