import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, CheckCircle2, KeyRound, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useSearchParams } from "react-router-dom";
import { z } from "zod";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PasswordInput } from "@/components/ui/password-input";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";
import { ApiError } from "@/lib/api/client";
import { rootflowApi } from "@/lib/api/rootflow-api";

const resetPasswordSchema = z
  .object({
    newPassword: z.string().min(8, "Password must be at least 8 characters.").max(128, "Password is too long."),
    confirmPassword: z.string().min(8, "Confirm your new password."),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token")?.trim() ?? "";
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const form = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: {
      newPassword: "",
      confirmPassword: "",
    },
  });

  async function handleSubmit(values: ResetPasswordFormValues) {
    setErrorMessage(null);

    try {
      const response = await rootflowApi.resetPassword({
        token,
        newPassword: values.newPassword,
      });

      setSuccessMessage(response.message);
      form.reset();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "We could not reset the password right now.");
    }
  }

  const isMissingToken = token.length === 0;

  return (
    <AuthScaffold
      badge={
        <>
          <KeyRound className="size-3.5" />
          Secure reset
        </>
      }
      title="Choose a new RootFlow password."
      description="Reset links stay short-lived and single-use so the password update remains safe without changing the rest of the RootFlow auth architecture."
      highlights={[
        {
          title: "Time-bound token flow",
          description: "The reset token is validated server-side before the password changes, and the token is invalidated immediately after a successful reset.",
        },
        {
          title: "Same premium auth surface",
          description: "Recovery keeps the same RootFlow branding, feedback patterns, and password controls as the rest of the auth experience.",
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">
            <ShieldCheck className="size-3.5" />
            Password update
          </Badge>
          <CardTitle>Reset password</CardTitle>
          <CardDescription>Set a new password to regain access to your RootFlow workspace.</CardDescription>
        </CardHeader>
        <CardContent className="px-0 pb-0">
          {successMessage ? (
            <div className="space-y-5">
              <div className="rounded-[26px] border border-primary/18 bg-primary/8 px-5 py-4">
                <div className="flex items-start gap-3">
                  <CheckCircle2 className="mt-0.5 size-5 text-primary" />
                  <div className="space-y-1">
                    <p className="text-sm font-semibold text-foreground">Password updated</p>
                    <p className="text-sm leading-6 text-muted-foreground">{successMessage}</p>
                  </div>
                </div>
              </div>

              <Button className="w-full justify-between" asChild>
                <Link to="/auth/login">
                  Return to login
                  <ArrowRight />
                </Link>
              </Button>
            </div>
          ) : isMissingToken ? (
            <div className="space-y-5">
              <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                This reset link is invalid or incomplete. Request a new password reset email to continue.
              </div>
              <div className="flex flex-col gap-3">
                <Button className="w-full justify-between" asChild>
                  <Link to="/auth/forgot-password">
                    Request a new link
                    <ArrowRight />
                  </Link>
                </Button>
                <Button variant="outline" className="w-full" asChild>
                  <Link to="/auth/login">Back to login</Link>
                </Button>
              </div>
            </div>
          ) : (
            <form className="space-y-5" onSubmit={form.handleSubmit(handleSubmit)}>
              <div className="space-y-2">
                <label className="text-sm font-semibold text-foreground" htmlFor="newPassword">
                  New password
                </label>
                <PasswordInput
                  id="newPassword"
                  autoComplete="new-password"
                  placeholder="Choose a secure password"
                  disabled={form.formState.isSubmitting}
                  {...form.register("newPassword")}
                />
                {form.formState.errors.newPassword ? (
                  <p className="text-sm text-destructive">{form.formState.errors.newPassword.message}</p>
                ) : null}
              </div>

              <div className="space-y-2">
                <label className="text-sm font-semibold text-foreground" htmlFor="confirmPassword">
                  Confirm new password
                </label>
                <PasswordInput
                  id="confirmPassword"
                  autoComplete="new-password"
                  placeholder="Confirm your new password"
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
                  {form.formState.isSubmitting ? "Resetting password..." : "Update password"}
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
