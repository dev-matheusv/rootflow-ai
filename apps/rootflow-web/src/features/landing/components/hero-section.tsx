import { ArrowRight, MessageSquare, Zap } from "lucide-react";
import { Link } from "react-router-dom";

const WHATSAPP_URL = "https://wa.me/5519999687742?text=Quero%20entender%20como%20o%20RootFlow%20pode%20ajudar%20minha%20empresa";

export function HeroSection() {
  return (
    <section className="relative flex min-h-screen items-center overflow-hidden pt-16">
      {/* Background gradients */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute top-0 left-1/2 h-[600px] w-[900px] -translate-x-1/2 -translate-y-1/4 rounded-full bg-[#0f63ec]/10 blur-[120px]" />
        <div className="absolute top-1/3 -left-32 h-[400px] w-[400px] rounded-full bg-[#06b6d4]/8 blur-[100px]" />
        <div className="absolute right-0 bottom-0 h-[400px] w-[500px] rounded-full bg-[#0f63ec]/6 blur-[120px]" />
        {/* Subtle grid */}
        <div
          className="absolute inset-0 opacity-[0.03]"
          style={{
            backgroundImage:
              "linear-gradient(rgba(255,255,255,0.8) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.8) 1px, transparent 1px)",
            backgroundSize: "64px 64px",
          }}
        />
      </div>

      <div className="relative mx-auto w-full max-w-7xl px-4 py-24 sm:px-6 sm:py-32 lg:px-8">
        <div className="mx-auto max-w-4xl text-center">
          {/* Badge */}
          <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-[#0f63ec]/30 bg-[#0f63ec]/10 px-4 py-1.5">
            <Zap className="h-3.5 w-3.5 text-[#06b6d4]" />
            <span className="text-xs font-semibold tracking-wide text-[#06b6d4] uppercase">
              IA para o conhecimento da sua empresa
            </span>
          </div>

          {/* Headline */}
          <h1 className="font-display mb-6 text-4xl leading-[1.1] font-bold tracking-tight text-white sm:text-5xl lg:text-6xl xl:text-7xl">
            Transforme os documentos da sua empresa em{" "}
            <span className="relative inline-block">
              <span className="relative z-10 bg-gradient-to-r from-[#0f63ec] via-[#3b87f5] to-[#06b6d4] bg-clip-text text-transparent">
                respostas instantâneas
              </span>
            </span>{" "}
            para sua equipe
          </h1>

          {/* Subheadline */}
          <p className="mx-auto mb-10 max-w-2xl text-lg leading-relaxed text-white/60 sm:text-xl">
            Centralize o conhecimento da sua operação, reduza dúvidas repetidas e acelere o onboarding com um
            assistente de IA treinado nos seus próprios documentos.
          </p>

          {/* CTAs */}
          <div className="mb-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
            <Link
              to="/auth/signup"
              className="group flex items-center gap-2 rounded-2xl bg-[#0f63ec] px-7 py-3.5 text-base font-semibold text-white shadow-[0_0_40px_rgba(15,99,236,0.4)] transition-all duration-200 hover:-translate-y-0.5 hover:bg-[#0f63ec]/95 hover:shadow-[0_0_56px_rgba(15,99,236,0.55)]"
            >
              Começar teste grátis
              <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
            </Link>
            <a
              href={WHATSAPP_URL}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center gap-2 rounded-2xl border border-white/12 bg-white/5 px-7 py-3.5 text-base font-semibold text-white/90 backdrop-blur-sm transition-all duration-200 hover:-translate-y-0.5 hover:border-white/20 hover:bg-white/8"
            >
              <MessageSquare className="h-4 w-4" />
              Falar no WhatsApp
            </a>
          </div>

          {/* Trust microcopy */}
          <div className="flex flex-wrap items-center justify-center gap-x-6 gap-y-2 text-sm text-white/40">
            <span className="flex items-center gap-1.5">
              <span className="h-1 w-1 rounded-full bg-[#06b6d4]" />
              Sem cartão de crédito
            </span>
            <span className="flex items-center gap-1.5">
              <span className="h-1 w-1 rounded-full bg-[#06b6d4]" />
              Setup em minutos
            </span>
            <span className="flex items-center gap-1.5">
              <span className="h-1 w-1 rounded-full bg-[#06b6d4]" />
              Cancele quando quiser
            </span>
          </div>
        </div>

        {/* Product preview card */}
        <div className="relative mx-auto mt-20 max-w-4xl">
          <div className="absolute inset-0 rounded-2xl bg-gradient-to-b from-[#0f63ec]/20 to-transparent blur-2xl" />
          <div className="relative overflow-hidden rounded-2xl border border-white/[0.08] bg-[#0d1520]/80 shadow-[0_32px_80px_rgba(0,0,0,0.6)] backdrop-blur-sm">
            {/* Fake browser chrome */}
            <div className="flex items-center gap-2 border-b border-white/[0.06] bg-white/[0.03] px-4 py-3">
              <div className="h-3 w-3 rounded-full bg-[#ff5f57]" />
              <div className="h-3 w-3 rounded-full bg-[#ffbd2e]" />
              <div className="h-3 w-3 rounded-full bg-[#28c840]" />
              <div className="ml-3 flex-1 rounded-md bg-white/[0.06] px-3 py-1 text-xs text-white/30">
                app.rootflow.com.br/assistant
              </div>
            </div>
            {/* Fake product preview */}
            <div className="flex min-h-[360px] flex-col gap-4 p-6 sm:p-8">
              <div className="flex items-start gap-3">
                <div className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-white/[0.06]">
                  <span className="text-xs text-white/50">👤</span>
                </div>
                <div className="rounded-2xl rounded-tl-sm bg-white/[0.06] px-4 py-3 text-sm text-white/70">
                  Qual é o processo de onboarding para novos clientes?
                </div>
              </div>
              <div className="flex items-start gap-3">
                <div className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-[#0f63ec]/20">
                  <Zap className="h-4 w-4 text-[#06b6d4]" />
                </div>
                <div className="max-w-lg space-y-2 rounded-2xl rounded-tl-sm border border-[#0f63ec]/20 bg-[#0f63ec]/8 px-4 py-3 text-sm text-white/80">
                  <p>Com base no Manual de Processos da sua empresa, o onboarding segue estas etapas:</p>
                  <ol className="ml-4 list-decimal space-y-1 text-white/60">
                    <li>Reunião de kickoff com o cliente (dias 1–2)</li>
                    <li>Configuração do ambiente e acesso ao sistema (dia 3)</li>
                    <li>Treinamento inicial com o time de CS (dias 4–5)</li>
                    <li>Acompanhamento quinzenal nas primeiras 4 semanas</li>
                  </ol>
                  <p className="text-xs text-white/40">
                    Fonte: Manual de Processos v2.3 · Onboarding de Clientes
                  </p>
                </div>
              </div>
              <div className="mt-auto flex items-center gap-3 rounded-xl border border-white/[0.06] bg-white/[0.03] px-4 py-3">
                <span className="flex-1 text-sm text-white/25">Faça uma pergunta sobre sua operação...</span>
                <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-[#0f63ec]">
                  <ArrowRight className="h-3.5 w-3.5 text-white" />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
