import { useQueryClient } from "@tanstack/react-query";
import { createContext, useContext, useEffect, useState, type PropsWithChildren } from "react";

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
  logout: () => void;
  refresh: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: PropsWithChildren) {
  const queryClient = useQueryClient();
  const [storedSession, setStoredSession] = useState<StoredAuthSession | null>(() => getStoredAuthSession());
  const [status, setStatus] = useState<AuthStatus>(() => (getStoredAuthSession() ? "loading" : "unauthenticated"));

  useEffect(() => {
    return subscribeToAuthSession((nextSession) => {
      setStoredSession(nextSession);
      setStatus(nextSession ? "authenticated" : "unauthenticated");
      queryClient.clear();
    });
  }, [queryClient]);

  useEffect(() => {
    if (!storedSession) {
      setStatus("unauthenticated");
      return;
    }

    let isCancelled = false;
    setStatus("loading");

    void (async () => {
      try {
        const session = await rootflowApi.getCurrentSession();
        if (isCancelled) {
          return;
        }

        const refreshedSession: AuthResponse = {
          ...storedSession,
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
  }, [queryClient, storedSession?.token]);

  async function login(payload: LoginPayload) {
    const session = await rootflowApi.login(payload);
    persistAuthSession(session);
    setStoredSession(session);
    setStatus("authenticated");
    queryClient.clear();
  }

  async function signup(payload: SignupPayload) {
    const session = await rootflowApi.signup(payload);
    persistAuthSession(session);
    setStoredSession(session);
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

    setStatus("loading");

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
    logout,
    refresh,
  };

  return <AuthContext.Provider value={contextValue}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider.");
  }

  return context;
}
