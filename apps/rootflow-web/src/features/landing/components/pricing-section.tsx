import { Check, MessageSquare, Sparkles } from "lucide-react";
import { Link } from "react-router-dom";

import { useBillingPlansQuery } from "@/hooks/use-rootflow-data";
import type { BillingPlanSummary } from "@/lib/api/contracts";

const WHATSAPP_BUSINESS_URL =
  "https://wa.me/5519999687742?text=Quero%20entender%20como%20o%20RootFlow%20pode%20ajudar%20minha%20empresa";

// UI config por plan code — features e destaque são visuais, não vêm do backend
const PLAN_UI_CONFIG: Record<
  string,
  {
    features: string[];
    highlight: boolean;
    badge?: string;
    ctaType: "signup" | "whatsapp";
  }
> = {
  starter: {
    features: [
      "Até 3 usuários",
      "Upload de documentos",
      "Assistente de IA com RAG",
      "Histórico de conversas",
      "Suporte por e-mail",
    ],
    highlight: false,
    ctaType: "signup",
  },
  pro: {
    features: [
      "Até 15 usuários",
      "Documentos ilimitados",
      "Assistente de IA com RAG avançado",
      "Histórico completo",
      "Múltiplas bases de conhecimento",
      "Suporte prioritário",
    ],
    highlight: true,
    badge: "Mais popular",
    ctaType: "signup",
  },
  business: {
    features: [
      "Usuários ilimitados",
      "Documentos ilimitados",
      "Tudo do Pro",
      "Onboarding dedicado",
      "SLA garantido",
      "Suporte via WhatsApp",
    ],
    highlight: false,
    ctaType: "whatsapp",
  },
};

function formatPrice(value: number, currencyCode: string) {
  return new Intl.NumberFormat("pt-BR", {
    style: "currency",
    currency: currencyCode,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}

function PlanCard({ plan }: { plan: BillingPlanSummary }) {
  const ui = PLAN_UI_CONFIG[plan.code.toLowerCase()] ?? {
    features: [],
    highlight: false,
    ctaType: "signup" as const,
  };

  const maxUsersLabel =
    plan.maxUsers === -1 ? "Usuários ilimitados" : `Até ${plan.maxUsers} usuário${plan.maxUsers > 1 ? "s" : ""}`;

  const features = ui.features.length > 0 ? ui.features : [maxUsersLabel];

  return (
    <div
      className={`relative flex flex-col overflow-hidden rounded-2xl border transition-all duration-300 ${
        ui.highlight
          ? "border-[#0f63ec]/50 bg-gradient-to-b from-[#0f63ec]/12 to-[#0f63ec]/5 shadow-[0_0_60px_rgba(15,99,236,0.2)]"
          : "border-white/[0.07] bg-white/[0.025] hover:border-white/12"
      }`}
    >
      {ui.badge && (
        <div className="absolute top-0 right-0 left-0 flex justify-center">
          <div className="flex items-center gap-1.5 rounded-b-xl bg-[#0f63ec] px-4 py-1.5">
            <Sparkles className="h-3 w-3 text-white/80" />
            <span className="text-xs font-semibold text-white">{ui.badge}</span>
          </div>
        </div>
      )}

      <div className={`flex flex-1 flex-col p-7 ${ui.badge ? "pt-10" : ""}`}>
        <div className="mb-6">
          <h3 className="font-display mb-1 text-lg font-bold text-white">{plan.name}</h3>
          <div className="flex items-baseline gap-1">
            <span className="font-display text-4xl font-bold text-white">
              {formatPrice(plan.monthlyPrice, plan.currencyCode)}
            </span>
            <span className="text-sm text-white/40">/mês</span>
          </div>
        </div>

        <ul className="mb-8 flex-1 space-y-3">
          {features.map((f, i) => (
            <li key={i} className="flex items-start gap-2.5 text-sm text-white/65">
              <Check className="mt-0.5 h-4 w-4 shrink-0 text-[#06b6d4]" />
              {f}
            </li>
          ))}
        </ul>

        {ui.ctaType === "whatsapp" ? (
          <a
            href={WHATSAPP_BUSINESS_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center justify-center gap-2 rounded-xl border border-white/10 bg-white/6 py-3 text-sm font-semibold text-white transition-all hover:-translate-y-0.5 hover:border-white/20 hover:bg-white/10"
          >
            <MessageSquare className="h-4 w-4" />
            Falar com vendas
          </a>
        ) : (
          <Link
            to="/auth/signup"
            className={`flex items-center justify-center rounded-xl py-3 text-sm font-semibold transition-all hover:-translate-y-0.5 ${
              ui.highlight
                ? "bg-[#0f63ec] text-white shadow-[0_0_32px_rgba(15,99,236,0.4)] hover:bg-[#0f63ec]/95 hover:shadow-[0_0_44px_rgba(15,99,236,0.55)]"
                : "border border-white/10 bg-white/6 text-white hover:border-white/20 hover:bg-white/10"
            }`}
          >
            Começar grátis
          </Link>
        )}
      </div>
    </div>
  );
}

function PlanSkeleton({ highlight = false }: { highlight?: boolean }) {
  return (
    <div
      className={`animate-pulse rounded-2xl border p-7 ${
        highlight ? "border-[#0f63ec]/30 bg-[#0f63ec]/5" : "border-white/[0.07] bg-white/[0.025]"
      }`}
    >
      <div className="mb-4 h-5 w-20 rounded-lg bg-white/10" />
      <div className="mb-6 h-10 w-32 rounded-lg bg-white/10" />
      <div className="mb-3 space-y-3">
        {[...Array(4)].map((_, i) => (
          <div key={i} className="h-4 rounded bg-white/[0.06]" style={{ width: `${70 + (i % 3) * 10}%` }} />
        ))}
      </div>
      <div className="mt-8 h-11 rounded-xl bg-white/10" />
    </div>
  );
}

export function PricingSection() {
  const { data: plans, isLoading, isError } = useBillingPlansQuery();

  const activePlans = plans?.filter((p) => p.isActive) ?? [];

  return (
    <section id="precos" className="relative overflow-hidden py-24 sm:py-32">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute left-1/2 top-1/2 h-[600px] w-[800px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-[#0f63ec]/8 blur-[160px]" />
      </div>

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto mb-16 max-w-2xl text-center">
          <p className="mb-3 text-sm font-semibold tracking-widest text-[#06b6d4] uppercase">Planos</p>
          <h2 className="font-display mb-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">
            Simples, transparente, sem surpresas
          </h2>
          <p className="text-base text-white/50 sm:text-lg">
            Comece grátis, sem cartão. Escolha o plano quando estiver pronto.
          </p>
        </div>

        <div className="grid gap-6 lg:grid-cols-3">
          {isLoading && (
            <>
              <PlanSkeleton />
              <PlanSkeleton highlight />
              <PlanSkeleton />
            </>
          )}

          {isError && (
            <div className="col-span-3 rounded-2xl border border-white/[0.07] bg-white/[0.02] p-8 text-center text-sm text-white/40">
              Não foi possível carregar os planos. Tente novamente em instantes.
            </div>
          )}

          {!isLoading && !isError && activePlans.map((plan) => <PlanCard key={plan.id} plan={plan} />)}
        </div>

        <p className="mt-8 text-center text-sm text-white/35">
          Todos os planos incluem 14 dias de teste grátis. Sem cartão de crédito. Cancele quando quiser.
        </p>
      </div>
    </section>
  );
}
