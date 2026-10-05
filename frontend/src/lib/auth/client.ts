import { sendJson, type ApiFailure } from "@/lib/api/client";

export type AuthResult = { ok: true } | ApiFailure;

const messages = {
  invalidCredentials: "Email or password is incorrect, or sign-in is blocked for a while after too many attempts.",
  emailTaken: "An account with this email already exists. Sign in instead.",
};

/** The auth endpoints, with messages that fit sign-in and registration (a 401 here means wrong credentials). */
export async function postAuth(action: "login" | "register" | "logout", body?: unknown): Promise<AuthResult> {
  const result = await sendJson("POST", `/api/auth/${action}`, body);

  if (result.ok) {
    return { ok: true };
  }
  if (result.status === 401) {
    return { ...result, message: messages.invalidCredentials };
  }
  if (result.status === 409) {
    return { ...result, message: messages.emailTaken };
  }
  return result;
}
