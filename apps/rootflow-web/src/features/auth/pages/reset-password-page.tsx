import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, CheckCircle2, KeyRound, ShieldCheck } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useSearchParams } from "react-router-dom";
import { z } from "zod";

import { useI18n } from "@/app/providers/i18n-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PasswordInput } from "@/components/ui/password-input";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";
import { ApiError } from "@/lib/api/client";
import { rootflowApi } from "@/lib/api/rootflow-api";

type ResetPasswordFormValues = {
  newPassword: string;
  confirmPassword: string;
};

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token")?.trim() ?? "";

  return <ResetPasswordContent key={token || "missing"} token={token} />;
}

interface ResetPasswordContentProps {
  token: string;
}

function ResetPasswordContent({ token }: ResetPasswordContentProps) {
  const { t } = useI18n();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [hasInvalidTokenError, setHasInvalidTokenError] = useState(false);
  const resetPasswordSchema = useMemo(
    () =>
      z
        .object({
          newPassword: z.string().min(8, t("auth.resetPassword.newPasswordValidation")).max(128, t("auth.resetPassword.passwordTooLong")),
          confirmPassword: z.string().min(8, t("auth.resetPassword.confirmPasswordValidation")),
        })
        .refine((values) => values.newPassword === values.confirmPassword, {
          message: t("auth.resetPassword.passwordsMismatch"),
          path: ["confirmPassword"],
        }),
    [t],
  );
  const form = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: {
      newPassword: "",
      confirmPassword: "",
    },
  });

  async function handleSubmit(values: ResetPasswordFormValues) {
    setErrorMessage(null);
    setHasInvalidTokenError(false);

    try {
      const response = await rootflowApi.resetPassword({
        token,
        newPassword: values.newPassword,
      });

      setSuccessMessage(response.message);
      form.reset();
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message);
        setHasInvalidTokenError(error.status === 400 && /invalid or has expired/i.test(error.message));
        return;
      }

      setErrorMessage(t("auth.resetPassword.fallbackError"));
    }
  }

  const isMissingToken = token.length === 0;
  const showInvalidTokenState = hasInvalidTokenError;

  return (
    <AuthScaffold
      badge={
        <>
          <KeyRound className="size-3.5" />
          {t("auth.resetPassword.badge")}
        </>
      }
      title={t("auth.resetPassword.title")}
      description={t("auth.resetPassword.description")}
      highlights={[
        {
          title: t("auth.resetPassword.highlightOneTitle"),
          description: t("auth.resetPassword.highlightOneDescription"),
        },
        {
          title: t("auth.resetPassword.highlightTwoTitle"),
          description: t("auth.resetPassword.highlightTwoDescription"),
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">
            <ShieldCheck className="size-3.5" />
            {t("common.labels.passwordUpdate")}
          </Badge>
          <CardTitle>{t("auth.resetPassword.cardTitle")}</CardTitle>
          <CardDescription>{t("auth.resetPassword.cardDescription")}</CardDescription>
        </CardHeader>
        <CardContent className="px-0 pb-0">
          {successMessage ? (
            <div className="space-y-5">
              <div className="rounded-[26px] border border-primary/18 bg-primary/8 px-5 py-4">
                <div className="flex items-start gap-3">
                  <CheckCircle2 className="mt-0.5 size-5 text-primary" />
                  <div className="space-y-1">
                    <p className="text-sm font-semibold text-foreground">{t("auth.resetPassword.passwordUpdated")}</p>
                    <p className="text-sm leading-6 text-muted-foreground">{successMessage}</p>
                  </div>
                </div>
              </div>

              <Button className="w-full justify-between" asChild>
                <Link to="/auth/login">
                  {t("common.actions.returnToLogin")}
                  <ArrowRight />
                </Link>
              </Button>
            </div>
          ) : isMissingToken || showInvalidTokenState ? (
            <div className="space-y-5">
              <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                {showInvalidTokenState
                  ? t("auth.resetPassword.invalidExpired")
                  : t("auth.resetPassword.invalidIncomplete")}
              </div>
              <div className="flex flex-col gap-3">
                <Button className="w-full justify-between" asChild>
                  <Link to="/auth/forgot-password">
                    {t("auth.resetPassword.requestNewLink")}
                    <ArrowRight />
                  </Link>
                </Button>
                <Button variant="outline" className="w-full" asChild>
                  <Link to="/auth/login">{t("common.actions.backToLogin")}</Link>
                </Button>
              </div>
            </div>
          ) : (
            <form className="space-y-5" onSubmit={form.handleSubmit(handleSubmit)}>
              <div className="space-y-2">
                <label className="text-sm font-semibold text-foreground" htmlFor="newPassword">
                  {t("auth.resetPassword.newPassword")}
                </label>
                <PasswordInput
                  id="newPassword"
                  autoComplete="new-password"
                  placeholder={t("auth.resetPassword.passwordPlaceholder")}
                  disabled={form.formState.isSubmitting}
                  {...form.register("newPassword")}
                />
                {form.formState.errors.newPassword ? (
                  <p className="text-sm text-destructive">{form.formState.errors.newPassword.message}</p>
                ) : null}
              </div>

              <div className="space-y-2">
                <label className="text-sm font-semibold text-foreground" htmlFor="confirmPassword">
                  {t("auth.resetPassword.confirmNewPassword")}
                </label>
                <PasswordInput
                  id="confirmPassword"
                  autoComplete="new-password"
                  placeholder={t("auth.resetPassword.confirmPasswordPlaceholder")}
                  disabled={form.formState.isSubmitting}
                  {...form.register("confirmPassword")}
                />
                {form.formState.errors.confirmPassword ? (
                  <p className="text-sm text-destructive">{form.formState.errors.confirmPassword.message}</p>
                ) : null}
              </div>

              {errorMessage ? (
                <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                  {errorMessage}
                </div>
              ) : null}

              <div className="flex flex-col gap-3">
                <Button type="submit" className="w-full justify-between" disabled={form.formState.isSubmitting}>
                  {form.formState.isSubmitting ? t("auth.resetPassword.resettingPassword") : t("auth.resetPassword.updatePassword")}
                  <ArrowRight />
                </Button>
                <Button variant="outline" className="w-full" asChild>
                  <Link to="/auth/login">{t("common.actions.backToLogin")}</Link>
                </Button>
              </div>
            </form>
          )}
        </CardContent>
      </Card>
    </AuthScaffold>
  );
}
