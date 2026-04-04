import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, ShieldCheck, Sparkles } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { z } from "zod";

import { useI18n } from "@/app/providers/i18n-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/ui/password-input";
import { useAuth } from "@/features/auth/auth-provider";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";
import { ApiError } from "@/lib/api/client";

type LoginFormValues = {
  email: string;
  password: string;
};

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();
  const { t } = useI18n();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const redirect = new URLSearchParams(location.search).get("redirect");
  const safeRedirect = redirect?.startsWith("/") ? redirect : "/dashboard";
  const loginSchema = useMemo(
    () =>
      z.object({
        email: z.email(t("auth.login.emailValidation")),
        password: z.string().min(8, t("auth.login.passwordValidation")),
      }),
    [t],
  );
  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: "",
      password: "",
    },
  });

  async function handleSubmit(values: LoginFormValues) {
    setErrorMessage(null);

    try {
      await login(values);
      navigate(safeRedirect, { replace: true });
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("auth.login.fallbackError"));
    }
  }

  return (
    <AuthScaffold
      badge={
        <>
          <ShieldCheck className="size-3.5" />
          {t("auth.login.badge")}
        </>
      }
      title={t("auth.login.title")}
      description={t("auth.login.description")}
      highlights={[
        {
          title: t("auth.login.highlightOneTitle"),
          description: t("auth.login.highlightOneDescription"),
        },
        {
          title: t("auth.login.highlightTwoTitle"),
          description: t("auth.login.highlightTwoDescription"),
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">
            <Sparkles className="size-3.5" />
            {t("common.labels.secureAccess")}
          </Badge>
          <CardTitle>{t("auth.login.cardTitle")}</CardTitle>
          <CardDescription>{t("auth.login.cardDescription")}</CardDescription>
        </CardHeader>
        <CardContent className="px-0 pb-0">
          <form className="space-y-5" onSubmit={form.handleSubmit(handleSubmit)}>
            <div className="space-y-2">
              <label className="text-sm font-semibold text-foreground" htmlFor="email">
                {t("common.labels.workEmail")}
              </label>
              <Input id="email" type="email" autoComplete="email" placeholder="team@company.com" {...form.register("email")} />
              {form.formState.errors.email ? (
                <p className="text-sm text-destructive">{form.formState.errors.email.message}</p>
              ) : null}
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between gap-3">
                <label className="text-sm font-semibold text-foreground" htmlFor="password">
                  {t("common.labels.password")}
                </label>
                <Link className="text-sm font-medium text-primary hover:text-primary/80" to="/auth/forgot-password">
                  {t("auth.login.forgotPassword")}
                </Link>
              </div>
              <PasswordInput
                id="password"
                autoComplete="current-password"
                placeholder={t("auth.login.passwordPlaceholder")}
                disabled={form.formState.isSubmitting}
                {...form.register("password")}
              />
              {form.formState.errors.password ? (
                <p className="text-sm text-destructive">{form.formState.errors.password.message}</p>
              ) : null}
            </div>

            {errorMessage ? (
              <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                {errorMessage}
              </div>
            ) : null}

            <div className="flex flex-col gap-3">
              <Button type="submit" className="w-full justify-between" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t("auth.login.submitting") : t("auth.login.submit")}
                <ArrowRight />
              </Button>
              <Button variant="outline" className="w-full" asChild>
                <Link to="/auth/signup">{t("auth.login.createWorkspace")}</Link>
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </AuthScaffold>
  );
}
