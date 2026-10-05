// Browser-side calls to the auth endpoints. They go through the /api rewrite (next.config.ts), so the session cookie
// is set on this site's own origin and the browser needs no CORS.

export type AuthResult =
  | { ok: true }
  | {
      ok: false;
      message: string;
      /** Messages per input, keyed by lower-case field name ("email", "password", "fullname"). */
      fieldErrors: Record<string, string>;
    };

type ProblemDetails = {
  detail?: string;
  errors?: Record<string, string[]>;
};

const messages = {
  invalidCredentials: "Email or password is incorrect, or sign-in is blocked for a while after too many attempts.",
  emailTaken: "An account with this email already exists. Sign in instead.",
  network: "Could not reach the server. Check your connection and try again.",
  server: "Something went wrong on our side. Try again in a moment.",
  invalidInput: "Check the highlighted fields and try again.",
};

export async function postAuth(action: "login" | "register" | "logout", body?: unknown): Promise<AuthResult> {
  let response: Response;
  try {
    response = await fetch(`/api/auth/${action}`, {
      method: "POST",
      headers: body === undefined ? undefined : { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    return failure(messages.network);
  }

  if (response.ok) {
    return { ok: true };
  }

  const problem = await readProblem(response);

  switch (response.status) {
    case 401:
      return failure(messages.invalidCredentials);
    case 409:
      return failure(messages.emailTaken);
    case 400:
      return badRequest(problem);
    default:
      return failure(messages.server);
  }
}

function failure(message: string, fieldErrors: Record<string, string> = {}): AuthResult {
  return { ok: false, message, fieldErrors };
}

function badRequest(problem: ProblemDetails | null): AuthResult {
  if (problem?.errors) {
    const fieldErrors: Record<string, string> = {};
    for (const [field, errors] of Object.entries(problem.errors)) {
      fieldErrors[field.toLowerCase()] = errors[0];
    }
    return failure(messages.invalidInput, fieldErrors);
  }

  // Business rule rejected by the API, e.g. the password policy: its message is already written for people.
  return failure(problem?.detail ?? messages.invalidInput);
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
}
