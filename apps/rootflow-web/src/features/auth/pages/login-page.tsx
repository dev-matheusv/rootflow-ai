import { ShieldCheck } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export function LoginPage() {
  return (
    <div className="mx-auto flex min-h-screen max-w-3xl items-center px-4 py-12">
      <Card className="w-full">
        <CardHeader>
          <Badge className="w-fit">
            <ShieldCheck className="size-3.5" />
            Future auth route
          </Badge>
          <CardTitle>Authentication will live here later.</CardTitle>
          <CardDescription>
            The frontend route structure is already prepared for login and account access without adding backend authentication yet.
          </CardDescription>
        </CardHeader>
        <CardContent className="text-sm leading-7 text-muted-foreground">
          This placeholder keeps the product architecture clean so authentication can be added without restructuring the app shell later.
        </CardContent>
      </Card>
    </div>
  );
}
