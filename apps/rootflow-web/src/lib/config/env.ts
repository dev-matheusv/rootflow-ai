type ApiBaseUrlSource = "env" | "missing";

function normalizeApiBaseUrl(value: string) {
  return value.replace(/\/+$/, "");
}

function resolveApiBaseUrl() {
  const configuredApiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim();

  if (configuredApiBaseUrl) {
    return {
      apiBaseUrl: normalizeApiBaseUrl(configuredApiBaseUrl),
      apiBaseUrlSource: "env" as ApiBaseUrlSource,
    };
  }

  return {
    apiBaseUrl: "",
    apiBaseUrlSource: "missing" as ApiBaseUrlSource,
  };
}

const resolvedApiConfig = resolveApiBaseUrl();
const apiConfigurationError = resolvedApiConfig.apiBaseUrl
  ? null
  : "VITE_API_BASE_URL is not configured. Copy apps/rootflow-web/.env.example to .env.local for local development or set the variable in your deploy target.";

if (import.meta.env.DEV && apiConfigurationError) {
  console.warn(`[RootFlow Web] ${apiConfigurationError}`);
}

export const env = {
  apiBaseUrl: resolvedApiConfig.apiBaseUrl,
  apiBaseUrlSource: resolvedApiConfig.apiBaseUrlSource,
  isApiBaseUrlExplicit: resolvedApiConfig.apiBaseUrlSource === "env",
  isApiBaseUrlConfigured: Boolean(resolvedApiConfig.apiBaseUrl),
  apiConfigurationError,
};
