import { useQueryClient } from "@tanstack/react-query";
import { createContext, useCallback, useContext, useEffect, useRef, useState, type PropsWithChildren } from "react";

import type { AuthResponse, LoginPayload, SessionInfo, SignupPayload } from "@/lib/api/contracts";
import { rootflowApi } from "@/lib/api/rootflow-api";
import {
  clearStoredAuthSession,
  getStoredAuthSession,
  persistAuthSession,
  subscribeToAuthSession,
  type StoredAuthSession,
} from "@/lib/auth/session-storage";

type AuthStatus = "loading" | "authenticated" | "unauthenticated";

interface AuthContextValue {
  status: AuthStatus;
  isAuthenticated: boolean;
  session: SessionInfo | null;
  token: string | null;
  expiresAtUtc: string | null;
  login: (payload: LoginPayload) => Promise<void>;
  signup: (payload: SignupPayload) => Promise<void>;
  applyAuthResponse: (response: AuthResponse) => void;
  logout: () => void;
  refresh: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);
const SESSION_REVALIDATION_THROTTLE_MS = 5_000;

function getSessionIdentity(session: StoredAuthSession | null) {
  if (!session) {
    return null;
  }

  return `${session.token}:${session.session.user.id}:${session.session.workspace.id}`;
}

export function AuthProvider({ children }: PropsWithChildren) {
  const queryClient = useQueryClient();
  const [storedSession, setStoredSession] = useState<StoredAuthSession | null>(() => getStoredAuthSession());
  const [status, setStatus] = useState<AuthStatus>(() => (getStoredAuthSession() ? "loading" : "unauthenticated"));
  const sessionIdentityRef = useRef<string | null>(getSessionIdentity(getStoredAuthSession()));
  const lastRevalidationAtRef = useRef(0);
  const refreshInFlightRef = useRef<Promise<void> | null>(null);

  useEffect(() => {
    return subscribeToAuthSession((nextSession) => {
      const nextIdentity = getSessionIdentity(nextSession);
      const previousIdentity = sessionIdentityRef.current;

      sessionIdentityRef.current = nextIdentity;
      setStoredSession(nextSession);
      setStatus(nextSession ? "authenticated" : "unauthenticated");

      if (previousIdentity !== nextIdentity) {
        queryClient.clear();
      }
    });
  }, [queryClient]);

  const synchronizeSession = useCallback(
    async (options?: { expectedToken?: string; clearOnFailure?: boolean }) => {
      const currentSession = getStoredAuthSession();
      const expectedToken = options?.expectedToken;

      if (!currentSession) {
        if (options?.clearOnFailure) {
          clearStoredAuthSession();
          setStoredSession(null);
          setStatus("unauthenticated");
          queryClient.clear();
        }
        return;
      }

      if (expectedToken && currentSession.token !== expectedToken) {
        return;
      }

      try {
        const session = await rootflowApi.getCurrentSession();
        const latestSession = getStoredAuthSession();
        if (!latestSession || (expectedToken && latestSession.token !== expectedToken)) {
          return;
        }

        const refreshedSession: AuthResponse = {
          ...latestSession,
          session,
        };

        persistAuthSession(refreshedSession);
        setStoredSession(refreshedSession);
        setStatus("authenticated");
      } catch {
        if (!options?.clearOnFailure) {
          return;
        }

        clearStoredAuthSession();
        setStoredSession(null);
        setStatus("unauthenticated");
        queryClient.clear();
      }
    },
    [queryClient],
  );

  useEffect(() => {
    const token = storedSession?.token;

    if (status !== "loading" || !token) {
      return;
    }

    let isCancelled = false;

    void (async () => {
      await synchronizeSession({ expectedToken: token, clearOnFailure: true });
      if (isCancelled) {
        return;
      }
    })();

    return () => {
      isCancelled = true;
    };
  }, [status, storedSession?.token, synchronizeSession]);

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    const revalidateOnForeground = () => {
      if (status === "loading" || document.visibilityState === "hidden") {
        return;
      }

      const currentSession = getStoredAuthSession();
      if (!currentSession) {
        return;
      }

      const now = Date.now();
      if (now - lastRevalidationAtRef.current < SESSION_REVALIDATION_THROTTLE_MS || refreshInFlightRef.current) {
        return;
      }

      lastRevalidationAtRef.current = now;
      const refreshPromise = synchronizeSession({ expectedToken: currentSession.token, clearOnFailure: true }).finally(() => {
        if (refreshInFlightRef.current === refreshPromise) {
          refreshInFlightRef.current = null;
        }
      });

      refreshInFlightRef.current = refreshPromise;
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState === "visible") {
        revalidateOnForeground();
      }
    };

    window.addEventListener("focus", revalidateOnForeground);
    window.addEventListener("pageshow", revalidateOnForeground);
    document.addEventListener("visibilitychange", handleVisibilityChange);

    return () => {
      window.removeEventListener("focus", revalidateOnForeground);
      window.removeEventListener("pageshow", revalidateOnForeground);
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, [status, synchronizeSession]);

  async function login(payload: LoginPayload) {
    applyAuthResponse(await rootflowApi.login(payload));
  }

  async function signup(payload: SignupPayload) {
    applyAuthResponse(await rootflowApi.signup(payload));
  }

  function applyAuthResponse(response: AuthResponse) {
    persistAuthSession(response);
    setStoredSession(response);
    setStatus("authenticated");
    queryClient.clear();
  }

  function logout() {
    clearStoredAuthSession();
    setStoredSession(null);
    setStatus("unauthenticated");
    queryClient.clear();
  }

  async function refresh() {
    await synchronizeSession({ clearOnFailure: true });
  }

  const contextValue: AuthContextValue = {
    status,
    isAuthenticated: status === "authenticated" && Boolean(storedSession),
    session: storedSession?.session ?? null,
    token: storedSession?.token ?? null,
    expiresAtUtc: storedSession?.expiresAtUtc ?? null,
    login,
    signup,
    applyAuthResponse,
    logout,
    refresh,
  };

  return <AuthContext.Provider value={contextValue}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider.");
  }

  return context;
}
