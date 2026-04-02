import type { PropsWithChildren } from "react";
import { Navigate, useLocation } from "react-router-dom";

import { RootFlowBrand } from "@/components/branding/rootflow-brand";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useAuth } from "@/features/auth/auth-provider";

export function RequireAuth({ children }: PropsWithChildren) {
  const location = useLocation();
  const { isAuthenticated, status } = useAuth();

  if (status === "loading") {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background px-4">
        <Card className="w-full max-w-md">
          <CardHeader>
            <RootFlowBrand variant="mark" size="md" className="mb-2 h-10" />
            <CardTitle>Restoring your workspace session</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm leading-6 text-muted-foreground">
              RootFlow is validating your token and reloading the current workspace context.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (!isAuthenticated) {
    const redirectTo = `${location.pathname}${location.search}${location.hash}`;
    return <Navigate replace to={`/auth/login?redirect=${encodeURIComponent(redirectTo)}`} />;
  }

  return <>{children}</>;
}
