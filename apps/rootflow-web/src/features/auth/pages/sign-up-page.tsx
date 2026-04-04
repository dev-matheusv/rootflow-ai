import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, Sparkles, UserRoundPlus } from "lucide-react";
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

type SignUpFormValues = {
  fullName: string;
  email: string;
  password: string;
  workspaceName: string;
};

export function SignUpPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { signup } = useAuth();
  const { t } = useI18n();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const redirect = new URLSearchParams(location.search).get("redirect");
  const safeRedirect = redirect?.startsWith("/") ? redirect : "/dashboard";
  const signUpSchema = useMemo(
    () =>
      z.object({
        fullName: z.string().trim().min(2, t("auth.signup.fullNameValidation")).max(120, t("auth.signup.fullNameTooLong")),
        email: z.email(t("auth.signup.emailValidation")),
        password: z.string().min(8, t("auth.signup.passwordValidation")).max(128, t("auth.signup.passwordTooLong")),
        workspaceName: z.string().trim().min(2, t("auth.signup.workspaceValidation")).max(120, t("auth.signup.workspaceTooLong")),
      }),
    [t],
  );
  const form = useForm<SignUpFormValues>({
    resolver: zodResolver(signUpSchema),
    defaultValues: {
      fullName: "",
      email: "",
      password: "",
      workspaceName: "",
    },
  });

  async function handleSubmit(values: SignUpFormValues) {
    setErrorMessage(null);

    try {
      await signup(values);
      navigate(safeRedirect, { replace: true });
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("auth.signup.fallbackError"));
    }
  }

  return (
    <AuthScaffold
      badge={
        <>
          <UserRoundPlus className="size-3.5" />
          {t("auth.signup.badge")}
        </>
      }
      title={t("auth.signup.title")}
      description={t("auth.signup.description")}
      highlights={[
        {
          title: t("auth.signup.highlightOneTitle"),
          description: t("auth.signup.highlightOneDescription"),
        },
        {
          title: t("auth.signup.highlightTwoTitle"),
          description: t("auth.signup.highlightTwoDescription"),
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">
            <Sparkles className="size-3.5" />
            {t("auth.signup.onboarding")}
          </Badge>
          <CardTitle>{t("auth.signup.cardTitle")}</CardTitle>
          <CardDescription>{t("auth.signup.cardDescription")}</CardDescription>
        </CardHeader>
        <CardContent className="px-0 pb-0">
          <form className="space-y-5" onSubmit={form.handleSubmit(handleSubmit)}>
            <div className="space-y-2">
              <label className="text-sm font-semibold text-foreground" htmlFor="fullName">
                {t("common.labels.fullName")}
              </label>
              <Input id="fullName" autoComplete="name" placeholder={t("auth.signup.fullNamePlaceholder")} {...form.register("fullName")} />
              {form.formState.errors.fullName ? (
                <p className="text-sm text-destructive">{form.formState.errors.fullName.message}</p>
              ) : null}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-semibold text-foreground" htmlFor="workspaceName">
                {t("common.labels.workspaceName")}
              </label>
              <Input id="workspaceName" autoComplete="organization" placeholder={t("auth.signup.workspacePlaceholder")} {...form.register("workspaceName")} />
              {form.formState.errors.workspaceName ? (
                <p className="text-sm text-destructive">{form.formState.errors.workspaceName.message}</p>
              ) : null}
            </div>

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
              <label className="text-sm font-semibold text-foreground" htmlFor="password">
                {t("common.labels.password")}
              </label>
              <PasswordInput
                id="password"
                autoComplete="new-password"
                placeholder={t("auth.signup.passwordPlaceholder")}
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
                {form.formState.isSubmitting ? t("auth.signup.submitting") : t("auth.signup.submit")}
                <ArrowRight />
              </Button>
              <Button variant="outline" className="w-full" asChild>
                <Link to="/auth/login">{t("auth.signup.alreadyHaveAccount")}</Link>
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </AuthScaffold>
  );
}
