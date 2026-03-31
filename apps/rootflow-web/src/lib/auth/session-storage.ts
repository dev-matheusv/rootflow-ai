import type { AuthResponse } from "@/lib/api/contracts";

const AUTH_STORAGE_KEY = "rootflow.auth.session";
const AUTH_SESSION_CHANGED_EVENT = "rootflow:auth-session-changed";

export type StoredAuthSession = AuthResponse;

let inMemorySession: StoredAuthSession | null | undefined;

export function getStoredAuthSession(): StoredAuthSession | null {
  if (inMemorySession !== undefined) {
    return inMemorySession;
  }

  inMemorySession = readStoredAuthSession();
  return inMemorySession;
}

export function persistAuthSession(session: StoredAuthSession | null) {
  inMemorySession = session;

  if (typeof window === "undefined") {
    return;
  }

  if (session) {
    window.localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
  } else {
    window.localStorage.removeItem(AUTH_STORAGE_KEY);
  }

  window.dispatchEvent(new Event(AUTH_SESSION_CHANGED_EVENT));
}

export function clearStoredAuthSession() {
  persistAuthSession(null);
}

export function subscribeToAuthSession(listener: (session: StoredAuthSession | null) => void) {
  if (typeof window === "undefined") {
    return () => undefined;
  }

  const handleSessionChanged = () => {
    inMemorySession = readStoredAuthSession();
    listener(inMemorySession);
  };

  const handleStorage = (event: StorageEvent) => {
    if (event.key === AUTH_STORAGE_KEY) {
      handleSessionChanged();
    }
  };

  window.addEventListener(AUTH_SESSION_CHANGED_EVENT, handleSessionChanged);
  window.addEventListener("storage", handleStorage);

  return () => {
    window.removeEventListener(AUTH_SESSION_CHANGED_EVENT, handleSessionChanged);
    window.removeEventListener("storage", handleStorage);
  };
}

function readStoredAuthSession(): StoredAuthSession | null {
  if (typeof window === "undefined") {
    return null;
  }

  const rawValue = window.localStorage.getItem(AUTH_STORAGE_KEY);
  if (!rawValue) {
    return null;
  }

  try {
    return JSON.parse(rawValue) as StoredAuthSession;
  } catch {
    window.localStorage.removeItem(AUTH_STORAGE_KEY);
    return null;
  }
}
