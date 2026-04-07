import { Check, MessageSquare, Sparkles } from "lucide-react";
import { Link } from "react-router-dom";

// TODO: Replace placeholder prices with dynamic values from /api/billing/plans
// Plan codes should match: "starter", "pro", "business" (or equivalent backend codes)
const PLANS = [
  {
    name: "Starter",
    price: "R$ 49,90",
    period: "/mês",
    description: "Ideal para times pequenos que querem começar a centralizar conhecimento.",
    features: [
      "Até 3 usuários",
      "Upload de documentos",
      "Assistente de IA com RAG",
      "Histórico de conversas",
      "Suporte por e-mail",
    ],
    cta: "Começar grátis",
    ctaHref: "/auth/signup",
    highlight: false,
  },
  {
    name: "Pro",
    price: "R$ 99,90",
    period: "/mês",
    description: "Para equipes em crescimento que precisam de mais capacidade e colaboração.",
    features: [
      "Até 15 usuários",
      "Documentos ilimitados",
      "Assistente de IA com RAG avançado",
      "Histórico completo",
      "Múltiplas bases de conhecimento",
      "Suporte prioritário",
    ],
    cta: "Começar grátis",
    ctaHref: "/auth/signup",
    highlight: true,
    badge: "Mais popular",
  },
  {
    name: "Business",
    price: "R$ 199,90",
    period: "/mês",
    description: "Para operações maiores com necessidades avançadas de conhecimento e controle.",
    features: [
      "Usuários ilimitados",
      "Documentos ilimitados",
      "Tudo do Pro",
      "Onboarding dedicado",
      "SLA garantido",
      "Suporte via WhatsApp",
    ],
    cta: "Falar com vendas",
    ctaHref: "https://wa.me/5519999687742?text=Quero%20entender%20como%20o%20RootFlow%20pode%20ajudar%20minha%20empresa",
    ctaExternal: true,
    highlight: false,
  },
];

export function PricingSection() {
  return (
    <section id="precos" className="relative overflow-hidden py-24 sm:py-32">
      {/* Background */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute left-1/2 top-1/2 h-[600px] w-[800px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-[#0f63ec]/8 blur-[160px]" />
      </div>

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mx-auto mb-16 max-w-2xl text-center">
          <p className="mb-3 text-sm font-semibold tracking-widest text-[#06b6d4] uppercase">Planos</p>
          <h2 className="font-display mb-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">
            Simples, transparente, sem surpresas
          </h2>
          <p className="text-base text-white/50 sm:text-lg">
            Comece grátis, sem cartão. Escolha o plano quando estiver pronto.
          </p>
        </div>

        {/* Plans */}
        <div className="grid gap-6 lg:grid-cols-3">
          {PLANS.map((plan, i) => (
            <div
              key={i}
              className={`relative flex flex-col overflow-hidden rounded-2xl border transition-all duration-300 ${
                plan.highlight
                  ? "border-[#0f63ec]/50 bg-gradient-to-b from-[#0f63ec]/12 to-[#0f63ec]/5 shadow-[0_0_60px_rgba(15,99,236,0.2)]"
                  : "border-white/[0.07] bg-white/[0.025] hover:border-white/12"
              }`}
            >
              {/* Popular badge */}
              {plan.badge && (
                <div className="absolute top-0 right-0 left-0 flex justify-center">
                  <div className="flex items-center gap-1.5 rounded-b-xl bg-[#0f63ec] px-4 py-1.5">
                    <Sparkles className="h-3 w-3 text-white/80" />
                    <span className="text-xs font-semibold text-white">{plan.badge}</span>
                  </div>
                </div>
              )}

              <div className={`flex flex-1 flex-col p-7 ${plan.badge ? "pt-10" : ""}`}>
                {/* Plan header */}
                <div className="mb-6">
                  <h3 className="font-display mb-1 text-lg font-bold text-white">{plan.name}</h3>
                  <p className="mb-4 text-sm text-white/45">{plan.description}</p>
                  <div className="flex items-baseline gap-1">
                    <span className="font-display text-4xl font-bold text-white">{plan.price}</span>
                    <span className="text-sm text-white/40">{plan.period}</span>
                  </div>
                </div>

                {/* Features */}
                <ul className="mb-8 flex-1 space-y-3">
                  {plan.features.map((f, j) => (
                    <li key={j} className="flex items-start gap-2.5 text-sm text-white/65">
                      <Check className="mt-0.5 h-4 w-4 shrink-0 text-[#06b6d4]" />
                      {f}
                    </li>
                  ))}
                </ul>

                {/* CTA */}
                {plan.ctaExternal ? (
                  <a
                    href={plan.ctaHref}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-center gap-2 rounded-xl border border-white/10 bg-white/6 py-3 text-sm font-semibold text-white transition-all hover:-translate-y-0.5 hover:border-white/20 hover:bg-white/10"
                  >
                    <MessageSquare className="h-4 w-4" />
                    {plan.cta}
                  </a>
                ) : (
                  <Link
                    to={plan.ctaHref}
                    className={`flex items-center justify-center rounded-xl py-3 text-sm font-semibold transition-all hover:-translate-y-0.5 ${
                      plan.highlight
                        ? "bg-[#0f63ec] text-white shadow-[0_0_32px_rgba(15,99,236,0.4)] hover:bg-[#0f63ec]/95 hover:shadow-[0_0_44px_rgba(15,99,236,0.55)]"
                        : "border border-white/10 bg-white/6 text-white hover:border-white/20 hover:bg-white/10"
                    }`}
                  >
                    {plan.cta}
                  </Link>
                )}
              </div>
            </div>
          ))}
        </div>

        {/* Bottom note */}
        <p className="mt-8 text-center text-sm text-white/35">
          Todos os planos incluem 14 dias de teste grátis. Sem cartão de crédito. Cancele quando quiser.
        </p>
      </div>
    </section>
  );
}
