import type { NextConfig } from "next";
import { BACKEND_URL } from "./src/lib/backend";

const nextConfig: NextConfig = {
  // The browser only talks to this site; /api/* is forwarded to the .NET API. No CORS, and the session cookie
  // belongs to this origin. Server Components call the API directly (see src/lib/auth/session.ts).
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${BACKEND_URL}/api/:path*` }];
  },
};

export default nextConfig;
