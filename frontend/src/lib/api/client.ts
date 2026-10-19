// Browser-side calls to the API. They go through the /api rewrite (next.config.ts), so the session cookie belongs
// to this site's own origin and the browser needs no CORS.

export type ApiFailure = {
  ok: false;
  /** HTTP status, or 0 when the server could not be reached. */
  status: number;
  message: string;
  /** Messages per input, keyed by lower-case field name (e.g. "email", "name"). */
  fieldErrors: Record<string, string>;
};

export type ApiResult<T> = { ok: true; data: T } | ApiFailure;

type ProblemDetails = {
  detail?: string;
  errors?: Record<string, string[]>;
};

const messages = {
  network: "Could not reach the server. Check your connection and try again.",
  server: "Something went wrong on our side. Try again in a moment.",
  invalidInput: "Check the highlighted fields and try again.",
  signedOut: "Your session has ended. Sign in again to continue.",
  forbidden: "You don't have permission to do this.",
  notFound: "This no longer exists, or you don't have access to it.",
  tooLarge: "This file is too large to upload.",
};

/** Reads the JSON of a GET. A request cancelled through `signal` is reported as `aborted`, which callers simply ignore. */
export async function getJson<T = unknown>(path: string, signal?: AbortSignal): Promise<ApiResult<T> | { aborted: true }> {
  return request<T>(path, { method: "GET", signal });
}

export async function sendJson<T = unknown>(
  method: "POST" | "PATCH" | "PUT" | "DELETE",
  path: string,
  body?: unknown,
): Promise<ApiResult<T>> {
  const result = await request<T>(path, {
    method,
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  return "aborted" in result ? failure(0, messages.network) : result;
}

/** Uploads a form (with files). No Content-Type is set: the browser adds the multipart boundary itself. */
export async function sendForm<T = unknown>(path: string, form: FormData): Promise<ApiResult<T>> {
  const result = await request<T>(path, { method: "POST", body: form });

  return "aborted" in result ? failure(0, messages.network) : result;
}

async function request<T>(path: string, init: RequestInit): Promise<ApiResult<T> | { aborted: true }> {
  let response: Response;
  try {
    response = await fetch(path, init);
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      return { aborted: true };
    }
    return failure(0, messages.network);
  }

  if (response.ok) {
    return { ok: true, data: (await readJson(response)) as T };
  }

  const problem = (await readJson(response)) as ProblemDetails | null;

  switch (response.status) {
    case 400:
      return badRequest(problem);
    case 401:
      return failure(401, messages.signedOut);
    case 403:
      return failure(403, messages.forbidden);
    case 404:
      return failure(404, messages.notFound);
    case 409:
      return failure(409, problem?.detail ?? messages.server);
    case 413:
      return failure(413, messages.tooLarge);
    default:
      return failure(response.status, messages.server);
  }
}

function failure(status: number, message: string, fieldErrors: Record<string, string> = {}): ApiFailure {
  return { ok: false, status, message, fieldErrors };
}

function badRequest(problem: ProblemDetails | null): ApiFailure {
  if (problem?.errors) {
    const fieldErrors: Record<string, string> = {};
    for (const [field, errors] of Object.entries(problem.errors)) {
      fieldErrors[field.toLowerCase()] = errors[0];
    }
    return failure(400, messages.invalidInput, fieldErrors);
  }

  return failure(400, problem?.detail ?? messages.invalidInput);
}

async function readJson(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch {
    return null; // e.g. 204 No Content
  }
}
