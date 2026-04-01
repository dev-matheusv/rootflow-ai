import { Server, TriangleAlert } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { env } from "@/lib/config/env";

export function ApiBaseUrlIndicator() {
  if (!import.meta.env.DEV) {
    return null;
  }

  const label = env.isApiBaseUrlConfigured ? "API env" : "API missing";
  const Icon = env.isApiBaseUrlConfigured ? Server : TriangleAlert;
  const title = env.isApiBaseUrlConfigured
    ? `Frontend API base URL (${label}): ${env.apiBaseUrl}`
    : env.apiConfigurationError ?? "VITE_API_BASE_URL is not configured.";
  const value = env.isApiBaseUrlConfigured ? env.apiBaseUrl : "Set VITE_API_BASE_URL";

  return (
    <Badge
      variant={env.isApiBaseUrlConfigured ? "secondary" : "warning"}
      className="max-w-full justify-start gap-1.5 px-3 py-1.5 text-[11px] font-medium normal-case tracking-normal"
      title={title}
    >
      <Icon className="size-3.5 shrink-0" />
      <span className="shrink-0 text-[10px] font-semibold uppercase tracking-[0.18em]">{label}</span>
      <span className="truncate">{value}</span>
    </Badge>
  );
}
