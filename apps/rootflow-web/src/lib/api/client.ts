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

export interface ApiErrorPayload {
  error?: string;
  code?: string;
}

interface RequestOptions extends RequestInit {
  parseAs?: "json" | "text" | "none";
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { parseAs = "json", headers, ...init } = options;
  const storedSession = getStoredAuthSession();

  if (!env.isApiBaseUrlConfigured) {
    throw new ApiError(env.apiConfigurationError ?? "The frontend API base URL is not configured.", 0);
  }

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

export function getApiErrorCode(error: unknown): string | null {
  if (!(error instanceof ApiError)) {
    return null;
  }

  const payload = error.payload;
  if (
    typeof payload === "object" &&
    payload !== null &&
    "code" in payload &&
    typeof payload.code === "string"
  ) {
    return payload.code;
  }

  return null;
}
