import { BarChart3, Clock, Shield, TrendingUp, Users, Zap } from "lucide-react";

const BENEFITS = [
  {
    icon: Clock,
    metric: "Menos interrupções",
    title: "Seu time para de depender de uma pessoa",
    description: "Quando a informação está disponível para todos, as interrupções caem — e o foco aumenta.",
  },
  {
    icon: TrendingUp,
    metric: "Onboarding mais rápido",
    title: "Novos colaboradores voam do primeiro dia",
    description: "Com acesso ao conhecimento da empresa, a curva de aprendizado cai pela metade.",
  },
  {
    icon: Users,
    metric: "Mais autonomia",
    title: "Cada pessoa encontra o que precisa",
    description: "Sem precisar esperar resposta de e-mail, ligação ou reunião. A resposta está no assistente.",
  },
  {
    icon: Shield,
    metric: "Respostas padronizadas",
    title: "Menos inconsistência, mais confiança",
    description: "Todos consultam a mesma fonte. As respostas são consistentes — seja para o cliente ou para o time.",
  },
  {
    icon: Zap,
    metric: "Operação mais ágil",
    title: "Decisões mais rápidas com menos atrito",
    description: "A informação certa na hora certa elimina o atrito operacional que trava o crescimento.",
  },
  {
    icon: BarChart3,
    metric: "Conhecimento escalável",
    title: "Cresça sem perder o que sabe",
    description: "À medida que a empresa cresce, o RootFlow cresce junto — absorvendo novos documentos e processos.",
  },
];

export function BenefitsSection() {
  return (
    <section className="relative overflow-hidden py-24 sm:py-32">
      {/* Background */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute inset-0 bg-gradient-to-b from-[#060d15] via-[#080e18] to-[#060d15]" />
        <div className="absolute left-1/2 top-0 h-px w-3/4 -translate-x-1/2 bg-gradient-to-r from-transparent via-white/10 to-transparent" />
      </div>

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mx-auto mb-16 max-w-2xl text-center">
          <p className="mb-3 text-sm font-semibold tracking-widest text-[#06b6d4] uppercase">Resultados</p>
          <h2 className="font-display mb-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">
            O que muda depois que você usa o RootFlow
          </h2>
          <p className="text-base text-white/50 sm:text-lg">
            Não é sobre tecnologia. É sobre o que acontece quando sua equipe para de perder tempo.
          </p>
        </div>

        {/* Benefits grid */}
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {BENEFITS.map((b, i) => (
            <div
              key={i}
              className="group relative overflow-hidden rounded-2xl border border-white/[0.06] bg-white/[0.025] p-6 transition-all duration-300 hover:border-[#0f63ec]/20 hover:bg-[#0f63ec]/5"
            >
              {/* Glow on hover */}
              <div className="pointer-events-none absolute inset-0 rounded-2xl bg-gradient-to-br from-[#0f63ec]/0 via-transparent to-transparent opacity-0 transition-opacity duration-300 group-hover:opacity-100" />

              <div className="relative">
                <div className="mb-4 flex h-10 w-10 items-center justify-center rounded-xl bg-[#0f63ec]/12">
                  <b.icon className="h-5 w-5 text-[#3b87f5]" />
                </div>
                <p className="mb-1 text-xs font-semibold tracking-wide text-[#06b6d4] uppercase">{b.metric}</p>
                <h3 className="mb-2 text-base font-semibold leading-snug text-white">{b.title}</h3>
                <p className="text-sm leading-relaxed text-white/50">{b.description}</p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
