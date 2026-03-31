import type { ReactNode } from "react";

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
      <div className="pointer-events-none absolute inset-0 opacity-[0.9]">
        <div className="absolute left-[-18%] top-[-20%] h-[34rem] w-[34rem] rounded-full bg-[radial-gradient(circle,_rgba(73,119,219,0.18),transparent_62%)] blur-3xl" />
        <div className="absolute right-[-12%] top-[16%] h-[28rem] w-[28rem] rounded-full bg-[radial-gradient(circle,_rgba(164,208,255,0.22),transparent_58%)] blur-3xl dark:bg-[radial-gradient(circle,_rgba(54,74,110,0.28),transparent_58%)]" />
        <div className="absolute bottom-[-18%] left-[24%] h-[24rem] w-[24rem] rounded-full bg-[radial-gradient(circle,_rgba(103,138,216,0.16),transparent_65%)] blur-3xl dark:bg-[radial-gradient(circle,_rgba(36,49,80,0.26),transparent_65%)]" />
      </div>

      <div className="relative mx-auto flex min-h-screen max-w-[1320px] items-center px-4 py-10 sm:px-6 lg:px-8">
        <div className="grid w-full gap-6 lg:grid-cols-[1.04fr_0.96fr]">
          <section className="rounded-[34px] border border-border/75 bg-card/86 p-6 shadow-[0_28px_70px_-48px_rgba(16,36,71,0.2)] backdrop-blur-2xl sm:p-8 lg:p-10">
            <Badge className="w-fit">{badge}</Badge>
            <div className="mt-6 max-w-2xl space-y-4">
              <h1 className="font-display text-4xl tracking-[-0.06em] text-foreground sm:text-[3.2rem]">{title}</h1>
              <p className="max-w-xl text-sm leading-7 text-muted-foreground sm:text-base">{description}</p>
            </div>

            <div className="mt-8 grid gap-4 sm:grid-cols-2">
              {highlights.map((highlight) => (
                <div key={highlight.title} className="rounded-[26px] border border-border/75 bg-background/76 p-5">
                  <div className="text-sm font-semibold tracking-[-0.01em] text-foreground">{highlight.title}</div>
                  <p className="mt-2 text-sm leading-6 text-muted-foreground">{highlight.description}</p>
                </div>
              ))}
            </div>
          </section>

          <section className="rounded-[34px] border border-border/75 bg-card/92 p-5 shadow-[0_28px_70px_-48px_rgba(16,36,71,0.2)] backdrop-blur-2xl sm:p-6 lg:p-8">
            {children}
          </section>
        </div>
      </div>
    </div>
  );
}
