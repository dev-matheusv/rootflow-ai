import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, CheckCircle2, MailQuestion, ShieldCheck } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { Link } from "react-router-dom";
import { z } from "zod";

import { useI18n } from "@/app/providers/i18n-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";
import { ApiError } from "@/lib/api/client";
import { rootflowApi } from "@/lib/api/rootflow-api";

type ForgotPasswordFormValues = {
  email: string;
};

export function ForgotPasswordPage() {
  const { t } = useI18n();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const forgotPasswordSchema = useMemo(
    () =>
      z.object({
        email: z.email(t("auth.forgotPassword.emailValidation")),
      }),
    [t],
  );
  const form = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: {
      email: "",
    },
  });

  async function handleSubmit(values: ForgotPasswordFormValues) {
    setErrorMessage(null);

    try {
      const response = await rootflowApi.forgotPassword(values);
      setSuccessMessage(response.message);
      form.reset(values);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("auth.forgotPassword.fallbackError"));
    }
  }

  return (
    <AuthScaffold
      badge={
        <>
          <MailQuestion className="size-3.5" />
          {t("auth.forgotPassword.badge")}
        </>
      }
      title={t("auth.forgotPassword.title")}
      description={t("auth.forgotPassword.description")}
      highlights={[
        {
          title: t("auth.forgotPassword.highlightOneTitle"),
          description: t("auth.forgotPassword.highlightOneDescription"),
        },
        {
          title: t("auth.forgotPassword.highlightTwoTitle"),
          description: t("auth.forgotPassword.highlightTwoDescription"),
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">
            <ShieldCheck className="size-3.5" />
            {t("common.labels.secureRecovery")}
          </Badge>
          <CardTitle>{t("auth.forgotPassword.cardTitle")}</CardTitle>
          <CardDescription>{t("auth.forgotPassword.cardDescription")}</CardDescription>
        </CardHeader>
        <CardContent className="px-0 pb-0">
          {successMessage ? (
            <div className="space-y-5">
              <div className="rounded-[26px] border border-primary/18 bg-primary/8 px-5 py-4">
                <div className="flex items-start gap-3">
                  <CheckCircle2 className="mt-0.5 size-5 text-primary" />
                  <div className="space-y-1">
                    <p className="text-sm font-semibold text-foreground">{t("auth.forgotPassword.checkInbox")}</p>
                    <p className="text-sm leading-6 text-muted-foreground">{successMessage}</p>
                  </div>
                </div>
              </div>

              <div className="flex flex-col gap-3">
                <Button type="button" className="w-full justify-between" onClick={() => setSuccessMessage(null)}>
                  {t("common.actions.sendAnotherLink")}
                  <ArrowRight />
                </Button>
                <Button variant="outline" className="w-full" asChild>
                  <Link to="/auth/login">{t("common.actions.backToLogin")}</Link>
                </Button>
              </div>
            </div>
          ) : (
            <form className="space-y-5" onSubmit={form.handleSubmit(handleSubmit)}>
              <div className="space-y-2">
                <label className="text-sm font-semibold text-foreground" htmlFor="email">
                  {t("common.labels.workEmail")}
                </label>
                <Input
                  id="email"
                  type="email"
                  autoComplete="email"
                  placeholder="team@company.com"
                  disabled={form.formState.isSubmitting}
                  {...form.register("email")}
                />
                {form.formState.errors.email ? (
                  <p className="text-sm text-destructive">{form.formState.errors.email.message}</p>
                ) : null}
              </div>

              {errorMessage ? (
                <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                  {errorMessage}
                </div>
              ) : null}

              <div className="flex flex-col gap-3">
                <Button type="submit" className="w-full justify-between" disabled={form.formState.isSubmitting}>
                  {form.formState.isSubmitting ? t("auth.forgotPassword.sendingLink") : t("auth.forgotPassword.sendResetLink")}
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
