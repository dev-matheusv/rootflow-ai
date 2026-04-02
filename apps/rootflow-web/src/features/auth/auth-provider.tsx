import { useQueryClient } from "@tanstack/react-query";
import { createContext, useContext, useEffect, useRef, useState, type PropsWithChildren } from "react";

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

  useEffect(() => {
    const token = storedSession?.token;

    if (status !== "loading" || !token) {
      return;
    }

    let isCancelled = false;

    void (async () => {
      try {
        const session = await rootflowApi.getCurrentSession();
        if (isCancelled) {
          return;
        }

        const currentSession = getStoredAuthSession();
        if (!currentSession || currentSession.token !== token) {
          return;
        }

        const refreshedSession: AuthResponse = {
          ...currentSession,
          session,
        };

        persistAuthSession(refreshedSession);
        setStoredSession(refreshedSession);
        setStatus("authenticated");
      } catch {
        if (isCancelled) {
          return;
        }

        clearStoredAuthSession();
        setStoredSession(null);
        setStatus("unauthenticated");
        queryClient.clear();
      }
    })();

    return () => {
      isCancelled = true;
    };
  }, [queryClient, status, storedSession?.token]);

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
    const existingSession = getStoredAuthSession();
    if (!existingSession) {
      logout();
      return;
    }

    try {
      const session = await rootflowApi.getCurrentSession();
      const refreshedSession: AuthResponse = {
        ...existingSession,
        session,
      };

      persistAuthSession(refreshedSession);
      setStoredSession(refreshedSession);
      setStatus("authenticated");
    } catch {
      logout();
    }
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
