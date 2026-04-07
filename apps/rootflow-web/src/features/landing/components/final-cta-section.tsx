import { ArrowRight, MessageSquare } from "lucide-react";
import { Link } from "react-router-dom";

const WHATSAPP_URL = "https://wa.me/5519999687742?text=Quero%20entender%20como%20o%20RootFlow%20pode%20ajudar%20minha%20empresa";

export function FinalCtaSection() {
  return (
    <section className="relative overflow-hidden py-24 sm:py-32">
      {/* Background */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute inset-0 bg-gradient-to-b from-[#060d15] to-[#060d15]" />
        <div className="absolute left-1/2 top-1/2 h-[700px] w-[900px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-[#0f63ec]/12 blur-[150px]" />
        {/* Top separator */}
        <div className="absolute top-0 left-1/2 h-px w-3/4 -translate-x-1/2 bg-gradient-to-r from-transparent via-white/10 to-transparent" />
      </div>

      <div className="relative mx-auto max-w-4xl px-4 text-center sm:px-6 lg:px-8">
        {/* Eyebrow */}
        <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-[#06b6d4]/25 bg-[#06b6d4]/8 px-4 py-1.5">
          <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-[#06b6d4]" />
          <span className="text-xs font-semibold text-[#06b6d4]">Pronto para começar?</span>
        </div>

        <h2 className="font-display mb-6 text-3xl font-bold tracking-tight text-white sm:text-4xl lg:text-5xl">
          Pare de perder tempo com{" "}
          <span className="bg-gradient-to-r from-[#0f63ec] to-[#06b6d4] bg-clip-text text-transparent">
            dúvidas repetidas
          </span>
        </h2>

        <p className="mx-auto mb-10 max-w-xl text-base leading-relaxed text-white/55 sm:text-lg">
          Centralize o conhecimento da sua empresa hoje. Sua equipe agradece amanhã.
        </p>

        {/* CTAs */}
        <div className="flex flex-col items-center justify-center gap-4 sm:flex-row">
          <Link
            to="/auth/signup"
            className="group flex w-full items-center justify-center gap-2 rounded-2xl bg-[#0f63ec] px-8 py-4 text-base font-semibold text-white shadow-[0_0_48px_rgba(15,99,236,0.4)] transition-all hover:-translate-y-0.5 hover:shadow-[0_0_64px_rgba(15,99,236,0.55)] sm:w-auto"
          >
            Começar teste grátis
            <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
          </Link>
          <a
            href={WHATSAPP_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="flex w-full items-center justify-center gap-2 rounded-2xl border border-white/12 bg-white/5 px-8 py-4 text-base font-semibold text-white/80 backdrop-blur-sm transition-all hover:-translate-y-0.5 hover:border-white/20 hover:bg-white/8 sm:w-auto"
          >
            <MessageSquare className="h-4 w-4" />
            Falar no WhatsApp
          </a>
        </div>

        {/* Microcopy */}
        <p className="mt-6 text-sm text-white/30">
          Sem cartão de crédito · 14 dias grátis · Setup em minutos
        </p>
      </div>
    </section>
  );
}
