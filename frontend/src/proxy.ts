import { NextResponse, type NextRequest } from "next/server";
import { SESSION_COOKIE } from "@/lib/auth/constants";

// Optimistic check only: it sends visitors without a session cookie to the login page before anything renders.
// A cookie that is present can still be expired or revoked, so pages call requireUser() (lib/auth/session.ts).
// This must never redirect away from /login based on the cookie alone: a stale cookie would loop /login <-> /.
export function proxy(request: NextRequest) {
  if (request.cookies.has(SESSION_COOKIE)) {
    return NextResponse.next();
  }

  const loginUrl = new URL("/login", request.url);
  const { pathname, search } = request.nextUrl;
  if (pathname !== "/") {
    loginUrl.searchParams.set("next", pathname + search);
  }

  return NextResponse.redirect(loginUrl);
}

export const config = {
  // Everything except the public pages (sign-in, sign-up and invitation links, which are opened before an account exists),
  // the API rewrite, Next internals and static files (anything with a dot).
  matcher: ["/((?!login$|register$|invite/|api/|_next/|.*\\..*).*)"],
};
