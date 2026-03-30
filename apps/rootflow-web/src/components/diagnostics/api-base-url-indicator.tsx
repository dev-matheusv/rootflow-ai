import { Server, TriangleAlert } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { env } from "@/lib/config/env";

export function ApiBaseUrlIndicator() {
  if (!import.meta.env.DEV) {
    return null;
  }

  const label = env.isApiBaseUrlExplicit ? "API env" : "API default";
  const Icon = env.isApiBaseUrlExplicit ? Server : TriangleAlert;

  return (
    <Badge
      variant={env.isApiBaseUrlExplicit ? "secondary" : "warning"}
      className="max-w-full justify-start gap-1.5 px-3 py-1.5 text-[11px] font-medium normal-case tracking-normal"
      title={`Frontend API base URL (${label}): ${env.apiBaseUrl}`}
    >
      <Icon className="size-3.5 shrink-0" />
      <span className="shrink-0 text-[10px] font-semibold uppercase tracking-[0.18em]">{label}</span>
      <span className="truncate">{env.apiBaseUrl}</span>
    </Badge>
  );
}
