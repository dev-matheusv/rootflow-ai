const localDevelopmentApiPort = 5011;

type ApiBaseUrlSource = "env" | "local-default" | "same-origin";

function normalizeApiBaseUrl(value: string) {
  return value.replace(/\/+$/, "");
}

function isLocalHostname(hostname: string) {
  return hostname === "localhost" || hostname === "127.0.0.1";
}

function resolveApiBaseUrl() {
  const configuredApiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim();

  if (configuredApiBaseUrl) {
    return {
      apiBaseUrl: normalizeApiBaseUrl(configuredApiBaseUrl),
      apiBaseUrlSource: "env" as ApiBaseUrlSource,
    };
  }

  if (typeof window !== "undefined" && isLocalHostname(window.location.hostname)) {
    const hostname = window.location.hostname;
    return {
      apiBaseUrl: normalizeApiBaseUrl(`http://${hostname}:${localDevelopmentApiPort}`),
      apiBaseUrlSource: "local-default" as ApiBaseUrlSource,
    };
  }

  return {
    apiBaseUrl: typeof window === "undefined" ? "" : normalizeApiBaseUrl(window.location.origin),
    apiBaseUrlSource: "same-origin" as ApiBaseUrlSource,
  };
}

const resolvedApiConfig = resolveApiBaseUrl();

if (import.meta.env.DEV && resolvedApiConfig.apiBaseUrlSource === "local-default") {
  console.warn(
    `[RootFlow Web] VITE_API_BASE_URL is not set. Using the local API default at ${resolvedApiConfig.apiBaseUrl}. Copy .env.example to .env.local to make the target explicit.`,
  );
}

export const env = {
  apiBaseUrl: resolvedApiConfig.apiBaseUrl,
  apiBaseUrlSource: resolvedApiConfig.apiBaseUrlSource,
  isApiBaseUrlExplicit: resolvedApiConfig.apiBaseUrlSource === "env",
  isUsingLocalApiFallback: resolvedApiConfig.apiBaseUrlSource === "local-default",
};
