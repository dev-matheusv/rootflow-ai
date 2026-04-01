import type { ReactNode } from "react";

import { RootFlowLogo } from "@/components/branding/rootflow-logo";
import { Badge } from "@/components/ui/badge";

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
  return (
    <div className="relative min-h-screen overflow-hidden bg-background text-foreground">
      <div className="pointer-events-none absolute inset-0 opacity-[0.88]">
        <div className="absolute left-[-16%] top-[-18%] h-[34rem] w-[34rem] rounded-full bg-[radial-gradient(circle,_rgba(73,119,219,0.16),transparent_62%)] blur-3xl" />
        <div className="absolute right-[-12%] top-[16%] h-[28rem] w-[28rem] rounded-full bg-[radial-gradient(circle,_rgba(164,208,255,0.18),transparent_58%)] blur-3xl dark:bg-[radial-gradient(circle,_rgba(54,74,110,0.28),transparent_58%)]" />
        <div className="absolute bottom-[-18%] left-[24%] h-[24rem] w-[24rem] rounded-full bg-[radial-gradient(circle,_rgba(103,138,216,0.14),transparent_65%)] blur-3xl dark:bg-[radial-gradient(circle,_rgba(36,49,80,0.26),transparent_65%)]" />
      </div>

      <div className="relative mx-auto flex min-h-screen max-w-[1320px] items-center px-4 py-10 sm:px-6 lg:px-8">
        <div className="grid w-full gap-6 lg:grid-cols-[1.04fr_0.96fr]">
          <section className="rounded-[34px] border border-border/70 bg-card/82 p-6 shadow-[0_28px_70px_-48px_rgba(16,36,71,0.16)] backdrop-blur-2xl sm:p-8 lg:p-10">
            <RootFlowLogo
              variant="banner"
              className="mb-6"
              tagline="Premium grounded knowledge for modern teams"
            />
            <Badge className="w-fit">{badge}</Badge>
            <div className="mt-6 max-w-2xl space-y-4">
              <h1 className="font-display text-4xl tracking-[-0.07em] text-foreground sm:text-[3.25rem]">{title}</h1>
              <p className="max-w-xl text-sm leading-7 text-muted-foreground sm:text-base">{description}</p>
            </div>

            <div className="mt-10 grid gap-4 sm:grid-cols-2">
              {highlights.map((highlight) => (
                <div key={highlight.title} className="rounded-[26px] border border-border/70 bg-background/72 p-5">
                  <div className="text-sm font-semibold tracking-[-0.01em] text-foreground">{highlight.title}</div>
                  <p className="mt-2 text-sm leading-6 text-muted-foreground">{highlight.description}</p>
                </div>
              ))}
            </div>
          </section>

          <section className="rounded-[34px] border border-border/70 bg-card/88 p-5 shadow-[0_28px_70px_-48px_rgba(16,36,71,0.16)] backdrop-blur-2xl sm:p-6 lg:p-8">
            {children}
          </section>
        </div>
      </div>
    </div>
  );
}
