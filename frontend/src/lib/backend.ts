// Where the .NET API lives. Used by the /api rewrite in next.config.ts and by server-side calls (lib/auth/session.ts).
export const BACKEND_URL = process.env.BACKEND_URL ?? "http://localhost:5051";
