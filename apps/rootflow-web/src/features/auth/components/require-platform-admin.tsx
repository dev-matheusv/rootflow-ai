import type { PropsWithChildren } from "react";
import { Navigate, useLocation } from "react-router-dom";

import { useAuth } from "@/features/auth/auth-provider";

export function RequirePlatformAdmin({ children }: PropsWithChildren) {
  const location = useLocation();
  const { isAuthenticated, session, status } = useAuth();

  if (status === "loading") {
    return null;
  }

  if (!isAuthenticated) {
    const redirectTo = `${location.pathname}${location.search}${location.hash}`;
    return <Navigate replace to={`/auth/login?redirect=${encodeURIComponent(redirectTo)}`} />;
  }

  if (!session?.isPlatformAdmin) {
    return <Navigate replace to="/dashboard" />;
  }

  return <>{children}</>;
}
