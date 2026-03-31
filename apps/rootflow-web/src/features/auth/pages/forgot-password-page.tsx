import { MailQuestion } from "lucide-react";
import { Link } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";

export function ForgotPasswordPage() {
  return (
    <AuthScaffold
      badge={
        <>
          <MailQuestion className="size-3.5" />
          Password recovery
        </>
      }
      title="Password recovery can stay minimal for this phase."
      description="The live auth foundation is in place. Recovery and verification flows can be layered on later without changing the session architecture."
      highlights={[
        {
          title: "Current scope",
          description: "Signup, login, JWT auth, and workspace isolation are live in this phase.",
        },
        {
          title: "Next layer",
          description: "Forgot-password email delivery can be added on top when account recovery becomes part of the roadmap.",
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">Placeholder route</Badge>
          <CardTitle>Reset flow not implemented yet</CardTitle>
          <CardDescription>Return to login or create a workspace for now.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3 px-0 pb-0">
          <Button asChild>
            <Link to="/auth/login">Back to login</Link>
          </Button>
          <Button variant="outline" asChild>
            <Link to="/auth/signup">Create workspace</Link>
          </Button>
        </CardContent>
      </Card>
    </AuthScaffold>
  );
}
