import { env } from "@/lib/config/env";
import { clearStoredAuthSession, getStoredAuthSession } from "@/lib/auth/session-storage";

export class ApiError extends Error {
  readonly status: number;
  readonly payload: unknown;

  constructor(message: string, status: number, payload?: unknown) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.payload = payload;
  }
}

interface RequestOptions extends RequestInit {
  parseAs?: "json" | "text" | "none";
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { parseAs = "json", headers, ...init } = options;
  const storedSession = getStoredAuthSession();

  const response = await fetch(`${env.apiBaseUrl}${path}`, {
    ...init,
    headers: {
      Accept: "application/json",
      ...(storedSession ? { Authorization: `Bearer ${storedSession.token}` } : {}),
      ...headers,
    },
  });

  if (!response.ok) {
    let payload: unknown = null;
    try {
      payload = await response.json();
    } catch {
      try {
        payload = await response.text();
      } catch {
        payload = null;
      }
    }

    const message =
      typeof payload === "object" && payload !== null && "error" in payload && typeof payload.error === "string"
        ? payload.error
        : `Request failed with status ${response.status}.`;

    if (response.status === 401 && storedSession) {
      clearStoredAuthSession();
    }

    throw new ApiError(message, response.status, payload);
  }

  if (parseAs === "none") {
    return undefined as T;
  }

  if (parseAs === "text") {
    return (await response.text()) as T;
  }

  return (await response.json()) as T;
}
