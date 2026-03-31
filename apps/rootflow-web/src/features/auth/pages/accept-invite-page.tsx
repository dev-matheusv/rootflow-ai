import { MailPlus } from "lucide-react";
import { Link } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";

export function AcceptInvitePage() {
  return (
    <AuthScaffold
      badge={
        <>
          <MailPlus className="size-3.5" />
          Invite route
        </>
      }
      title="Invites are intentionally out of scope for this phase."
      description="The membership model is now live, so invite acceptance can be added later without reworking the underlying workspace relationships."
      highlights={[
        {
          title: "Ready foundation",
          description: "Users, workspaces, and memberships are now real entities in the backend.",
        },
        {
          title: "Deferred workflow",
          description: "Invite issuance and acceptance stay postponed until after the core SaaS foundation settles.",
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">Placeholder route</Badge>
          <CardTitle>Invite acceptance is not enabled yet</CardTitle>
          <CardDescription>Use direct signup or login until invite workflows are added.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3 px-0 pb-0">
          <Button asChild>
            <Link to="/auth/signup">Create workspace</Link>
          </Button>
          <Button variant="outline" asChild>
            <Link to="/auth/login">Back to login</Link>
          </Button>
        </CardContent>
      </Card>
    </AuthScaffold>
  );
}
