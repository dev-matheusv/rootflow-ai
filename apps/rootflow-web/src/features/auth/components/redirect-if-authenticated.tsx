import type { PropsWithChildren } from "react";
import { Navigate, useLocation } from "react-router-dom";

import { useAuth } from "@/features/auth/auth-provider";

export function RedirectIfAuthenticated({ children }: PropsWithChildren) {
  const location = useLocation();
  const { isAuthenticated, status } = useAuth();
  const redirect = new URLSearchParams(location.search).get("redirect");
  const safeRedirect = redirect?.startsWith("/") ? redirect : "/dashboard";

  if (status === "loading") {
    return null;
  }

  if (isAuthenticated) {
    return <Navigate replace to={safeRedirect} />;
  }

  return <>{children}</>;
}
