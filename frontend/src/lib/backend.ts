// Where the gateway lives: the only door to the backend. Used by the /api rewrite in next.config.ts and by server-side calls (lib/api/server.ts).
export const BACKEND_URL = process.env.BACKEND_URL ?? "http://localhost:5050";
