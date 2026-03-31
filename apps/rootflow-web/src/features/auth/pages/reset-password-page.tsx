import { KeyRound } from "lucide-react";
import { Link } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";

export function ResetPasswordPage() {
  return (
    <AuthScaffold
      badge={
        <>
          <KeyRound className="size-3.5" />
          Reset route
        </>
      }
      title="Password reset links are intentionally deferred."
      description="This route is live so the navigation stays honest, but the actual tokenized reset flow remains outside the current SaaS foundation phase."
      highlights={[
        {
          title: "Stable now",
          description: "The app already supports local session persistence, logout, and protected routes.",
        },
        {
          title: "Later extension",
          description: "A reset token workflow can be added without changing the workspace auth contract.",
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">Placeholder route</Badge>
          <CardTitle>Reset password is not active yet</CardTitle>
          <CardDescription>Use the current login or signup flow for this phase.</CardDescription>
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
