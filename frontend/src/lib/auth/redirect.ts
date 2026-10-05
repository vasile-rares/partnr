/** Where to go after signing in. Only same-site paths are accepted, so `?next=` cannot send people to another site. */
export function safeNextPath(value: string | string[] | undefined): string {
  const path = Array.isArray(value) ? value[0] : value;

  const isSameSitePath = path?.startsWith("/") && !path.startsWith("//") && !path.startsWith("/\\");

  return isSameSitePath ? path! : "/";
}
