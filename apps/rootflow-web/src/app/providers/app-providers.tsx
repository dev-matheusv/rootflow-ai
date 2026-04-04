import type { PropsWithChildren } from "react";

import { I18nProvider } from "@/app/providers/i18n-provider";
import { QueryProvider } from "@/app/providers/query-provider";
import { ThemeProvider } from "@/app/providers/theme-provider";
import { AuthProvider } from "@/features/auth/auth-provider";

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <I18nProvider>
      <ThemeProvider>
        <QueryProvider>
          <AuthProvider>{children}</AuthProvider>
        </QueryProvider>
      </ThemeProvider>
    </I18nProvider>
  );
}
