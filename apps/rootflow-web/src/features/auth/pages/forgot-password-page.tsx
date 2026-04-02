import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, CheckCircle2, MailQuestion, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link } from "react-router-dom";
import { z } from "zod";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";
import { ApiError } from "@/lib/api/client";
import { rootflowApi } from "@/lib/api/rootflow-api";

const forgotPasswordSchema = z.object({
  email: z.email("Enter a valid email address."),
});

type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;

export function ForgotPasswordPage() {
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
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
      setErrorMessage(error instanceof ApiError ? error.message : "We could not start the reset flow right now.");
    }
  }

  return (
    <AuthScaffold
      badge={
        <>
          <MailQuestion className="size-3.5" />
          Password recovery
        </>
      }
      title="Reset your RootFlow password."
      description="Request a secure reset link without exposing account existence, then return to the same workspace-scoped access flow once your password is updated."
      highlights={[
        {
          title: "Neutral response by design",
          description: "The recovery request always returns the same message so RootFlow never reveals whether an email belongs to an account.",
        },
        {
          title: "Single-use reset links",
          description: "Reset tokens are time-bound, one-time, and delivered through RootFlow's shared outbound email configuration without changing the frontend flow.",
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">
            <ShieldCheck className="size-3.5" />
            Secure recovery
          </Badge>
          <CardTitle>Forgot password</CardTitle>
          <CardDescription>Enter your work email and RootFlow will send a reset link if the account exists.</CardDescription>
        </CardHeader>
        <CardContent className="px-0 pb-0">
          {successMessage ? (
            <div className="space-y-5">
              <div className="rounded-[26px] border border-primary/18 bg-primary/8 px-5 py-4">
                <div className="flex items-start gap-3">
                  <CheckCircle2 className="mt-0.5 size-5 text-primary" />
                  <div className="space-y-1">
                    <p className="text-sm font-semibold text-foreground">Check your inbox</p>
                    <p className="text-sm leading-6 text-muted-foreground">{successMessage}</p>
                  </div>
                </div>
              </div>

              <div className="flex flex-col gap-3">
                <Button type="button" className="w-full justify-between" onClick={() => setSuccessMessage(null)}>
                  Send another link
                  <ArrowRight />
                </Button>
                <Button variant="outline" className="w-full" asChild>
                  <Link to="/auth/login">Back to login</Link>
                </Button>
              </div>
            </div>
          ) : (
            <form className="space-y-5" onSubmit={form.handleSubmit(handleSubmit)}>
              <div className="space-y-2">
                <label className="text-sm font-semibold text-foreground" htmlFor="email">
                  Work email
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
                  {form.formState.isSubmitting ? "Sending link..." : "Send reset link"}
                  <ArrowRight />
                </Button>
                <Button variant="outline" className="w-full" asChild>
                  <Link to="/auth/login">Back to login</Link>
                </Button>
              </div>
            </form>
          )}
        </CardContent>
      </Card>
    </AuthScaffold>
  );
}
