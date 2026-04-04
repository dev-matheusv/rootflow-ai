import type { ReactNode } from "react";

import { useI18n } from "@/app/providers/i18n-provider";
import { RootFlowBrand } from "@/components/branding/rootflow-brand";
import { Badge } from "@/components/ui/badge";
import { LanguageSwitcher } from "@/components/ui/language-switcher";

interface AuthScaffoldProps {
  badge: ReactNode;
  title: string;
  description: string;
  highlights: Array<{
    title: string;
    description: string;
  }>;
  children: ReactNode;
}

export function AuthScaffold({ badge, title, description, highlights, children }: AuthScaffoldProps) {
  const { t } = useI18n();

  return (
    <div className="relative min-h-screen overflow-hidden bg-background text-foreground">
      <div className="pointer-events-none absolute inset-0 opacity-[0.82]">
        <div className="absolute left-[-16%] top-[-18%] h-[34rem] w-[34rem] rounded-full bg-[radial-gradient(circle,_rgba(73,119,219,0.14),transparent_62%)] blur-3xl" />
        <div className="absolute right-[-12%] top-[16%] h-[28rem] w-[28rem] rounded-full bg-[radial-gradient(circle,_rgba(164,208,255,0.14),transparent_58%)] blur-3xl dark:bg-[radial-gradient(circle,_rgba(54,74,110,0.24),transparent_58%)]" />
        <div className="absolute bottom-[-18%] left-[24%] h-[24rem] w-[24rem] rounded-full bg-[radial-gradient(circle,_rgba(103,138,216,0.1),transparent_65%)] blur-3xl dark:bg-[radial-gradient(circle,_rgba(36,49,80,0.22),transparent_65%)]" />
      </div>

      <div className="relative mx-auto flex min-h-screen max-w-[1320px] items-center px-4 py-10 sm:px-6 lg:px-8">
        <div className="grid w-full gap-6 lg:grid-cols-[1.04fr_0.96fr]">
          <section className="min-w-0 rounded-[32px] border border-border/65 bg-card/84 p-6 shadow-[0_24px_60px_-46px_rgba(16,36,71,0.14)] backdrop-blur-2xl sm:p-8 lg:p-10">
            <div className="relative mb-7 overflow-hidden rounded-[28px] bg-[linear-gradient(140deg,#08275f_0%,#0b438f_48%,#0e62d9_100%)] px-6 py-6 text-white shadow-[0_24px_56px_-48px_rgba(7,65,169,0.44)] sm:px-7 sm:py-7 lg:px-8 lg:py-8">
              <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_0%_0%,rgba(255,255,255,0.14),transparent_34%),radial-gradient(circle_at_100%_100%,rgba(255,255,255,0.06),transparent_26%)]" />
              <div className="relative grid gap-8 lg:grid-cols-[minmax(0,1fr)_minmax(220px,0.86fr)] lg:items-center">
                <div className="space-y-3 lg:max-w-sm">
                  <div className="text-[11px] font-semibold uppercase tracking-[0.28em] text-white/62">
                    {t("auth.brandEyebrow")}
                  </div>
                  <p className="max-w-sm text-sm leading-7 text-white/82 sm:text-base">
                    {t("auth.brandDescription")}
                  </p>
                </div>
                <div className="flex w-full justify-center pt-1 lg:justify-center lg:pt-0">
                  <RootFlowBrand
                    variant="mark"
                    size="lg"
                    className="h-32 max-w-none object-contain sm:h-36 lg:h-[12rem]"
                  />
                </div>
              </div>
            </div>
            <Badge className="w-fit">{badge}</Badge>
            <div className="mt-6 max-w-2xl space-y-4">
              <h1 className="font-display text-4xl tracking-[-0.07em] text-foreground sm:text-[3.25rem]">{title}</h1>
              <p className="max-w-xl text-sm leading-7 text-muted-foreground sm:text-base">{description}</p>
            </div>

            <div className="mt-10 grid gap-4 sm:grid-cols-2">
              {highlights.map((highlight) => (
                <div key={highlight.title} className="rounded-[24px] border border-border/65 bg-background/62 p-5">
                  <div className="text-sm font-semibold tracking-[-0.01em] text-foreground">{highlight.title}</div>
                  <p className="mt-2 text-sm leading-6 text-muted-foreground">{highlight.description}</p>
                </div>
              ))}
            </div>
          </section>

          <section className="min-w-0 rounded-[32px] border border-border/65 bg-card/88 p-5 shadow-[0_24px_60px_-46px_rgba(16,36,71,0.14)] backdrop-blur-2xl sm:p-6 lg:p-8">
            <div className="mb-7 flex flex-wrap items-center justify-between gap-3">
              <RootFlowBrand variant="logo" size="lg" className="h-[4.35rem] max-w-none sm:h-[4.75rem]" />
              <LanguageSwitcher />
            </div>
            {children}
          </section>
        </div>
      </div>
    </div>
  );
}
