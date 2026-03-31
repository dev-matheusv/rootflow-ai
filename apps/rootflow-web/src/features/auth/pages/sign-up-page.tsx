import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, Sparkles, UserRoundPlus } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { z } from "zod";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/features/auth/auth-provider";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";
import { ApiError } from "@/lib/api/client";

const signUpSchema = z.object({
  fullName: z.string().trim().min(2, "Enter your full name.").max(120, "Name is too long."),
  email: z.email("Enter a valid email address."),
  password: z.string().min(8, "Password must be at least 8 characters.").max(128, "Password is too long."),
  workspaceName: z.string().trim().min(2, "Enter a workspace name.").max(120, "Workspace name is too long."),
});

type SignUpFormValues = z.infer<typeof signUpSchema>;

export function SignUpPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { signup } = useAuth();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const redirect = new URLSearchParams(location.search).get("redirect");
  const safeRedirect = redirect?.startsWith("/") ? redirect : "/dashboard";
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
      setErrorMessage(error instanceof ApiError ? error.message : "We could not create your workspace right now.");
    }
  }

  return (
    <AuthScaffold
      badge={
        <>
          <UserRoundPlus className="size-3.5" />
          Create workspace
        </>
      }
      title="Provision a RootFlow workspace in one move."
      description="Create your account, spin up a workspace, and start as the owner with a live JWT session already attached to the right tenant."
      highlights={[
        {
          title: "Owner membership on signup",
          description: "RootFlow creates the user, workspace, and owner role together so the SaaS foundation starts cleanly.",
        },
        {
          title: "Ready for multi-user growth",
          description: "The underlying model now supports future admins, members, and shared workspaces without redesigning the shell.",
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">
            <Sparkles className="size-3.5" />
            Premium onboarding
          </Badge>
          <CardTitle>Sign up</CardTitle>
          <CardDescription>Start a new RootFlow workspace with secure email and password authentication.</CardDescription>
        </CardHeader>
        <CardContent className="px-0 pb-0">
          <form className="space-y-5" onSubmit={form.handleSubmit(handleSubmit)}>
            <div className="space-y-2">
              <label className="text-sm font-semibold text-foreground" htmlFor="fullName">
                Full name
              </label>
              <Input id="fullName" autoComplete="name" placeholder="Jordan Rivera" {...form.register("fullName")} />
              {form.formState.errors.fullName ? (
                <p className="text-sm text-destructive">{form.formState.errors.fullName.message}</p>
              ) : null}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-semibold text-foreground" htmlFor="workspaceName">
                Workspace name
              </label>
              <Input id="workspaceName" autoComplete="organization" placeholder="Acme Operations" {...form.register("workspaceName")} />
              {form.formState.errors.workspaceName ? (
                <p className="text-sm text-destructive">{form.formState.errors.workspaceName.message}</p>
              ) : null}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-semibold text-foreground" htmlFor="email">
                Work email
              </label>
              <Input id="email" type="email" autoComplete="email" placeholder="team@company.com" {...form.register("email")} />
              {form.formState.errors.email ? (
                <p className="text-sm text-destructive">{form.formState.errors.email.message}</p>
              ) : null}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-semibold text-foreground" htmlFor="password">
                Password
              </label>
              <Input
                id="password"
                type="password"
                autoComplete="new-password"
                placeholder="Choose a secure password"
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
                {form.formState.isSubmitting ? "Creating workspace..." : "Create workspace"}
                <ArrowRight />
              </Button>
              <Button variant="outline" className="w-full" asChild>
                <Link to="/auth/login">Already have an account?</Link>
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </AuthScaffold>
  );
}
