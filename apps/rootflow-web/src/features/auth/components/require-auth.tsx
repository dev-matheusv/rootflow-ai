import type { PropsWithChildren } from "react";
import { Navigate, useLocation } from "react-router-dom";

import { useI18n } from "@/app/providers/i18n-provider";
import { RootFlowBrand } from "@/components/branding/rootflow-brand";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useAuth } from "@/features/auth/auth-provider";

export function RequireAuth({ children }: PropsWithChildren) {
  const location = useLocation();
  const { isAuthenticated, status } = useAuth();
  const { t } = useI18n();

  if (status === "loading") {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background px-4">
        <Card className="w-full max-w-md">
          <CardHeader>
            <RootFlowBrand variant="logo" size="md" className="mb-3 h-14" />
            <CardTitle>{t("requireAuth.title")}</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm leading-6 text-muted-foreground">
              {t("requireAuth.description")}
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
