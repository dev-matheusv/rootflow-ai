import { ArrowRight, UserRoundPlus } from "lucide-react";
import { Link } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export function SignUpPage() {
  return (
    <div className="mx-auto flex min-h-screen max-w-4xl items-center px-4 py-12">
      <Card className="w-full overflow-hidden">
        <CardHeader className="relative gap-4">
          <div className="absolute inset-x-0 top-0 h-24 bg-[linear-gradient(135deg,rgba(37,99,235,0.08),transparent_70%)] dark:bg-[linear-gradient(135deg,rgba(138,180,255,0.12),transparent_70%)]" />
          <Badge className="w-fit">
            <UserRoundPlus className="size-3.5" />
            Sign up placeholder
          </Badge>
          <CardTitle>Sign up flow is ready for future activation.</CardTitle>
          <CardDescription>
            The UI now includes a dedicated entry point for future signup, onboarding, and workspace creation without needing to redesign the product shell later.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
          <div className="space-y-4 text-sm leading-7 text-muted-foreground">
            <div className="rounded-[24px] border border-border/80 bg-background/80 p-5">
              <div className="text-sm font-semibold text-foreground">What will live here</div>
              <p className="mt-2">
                Account creation, workspace provisioning, verification, and first-use onboarding can be connected here when backend authentication becomes available.
              </p>
            </div>
            <div className="rounded-[24px] border border-border/80 bg-background/80 p-5">
              <div className="text-sm font-semibold text-foreground">Why it matters now</div>
              <p className="mt-2">
                Showing the future auth structure makes the app feel more complete in demos and keeps the navigation ready for commercialization.
              </p>
            </div>
          </div>

          <div className="rounded-[28px] border border-border/80 bg-background/82 p-5 shadow-[0_18px_36px_-30px_rgba(16,36,71,0.16)] dark:shadow-[0_18px_36px_-30px_rgba(0,0,0,0.34)]">
            <div className="space-y-2">
              <div className="text-sm font-semibold text-foreground">Available next steps</div>
              <p className="text-sm leading-6 text-muted-foreground">
                Continue exploring the product or return to the login placeholder route.
              </p>
            </div>
            <div className="mt-5 flex flex-col gap-3">
              <Button asChild>
                <Link to="/dashboard">
                  Open RootFlow
                  <ArrowRight />
                </Link>
              </Button>
              <Button variant="outline" asChild>
                <Link to="/auth/login">Go to login</Link>
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
