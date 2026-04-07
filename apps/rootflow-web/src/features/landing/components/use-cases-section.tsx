import { BookOpen, Briefcase, HeadphonesIcon, Users } from "lucide-react";

const USE_CASES = [
  {
    icon: Briefcase,
    tag: "Agências e consultorias",
    title: "Playbooks e SOPs sempre acessíveis",
    description:
      "Sua equipe para de perguntar como fazer e começa a consultar. Processos, scripts de venda, guias de entrega e metodologias ficam disponíveis para qualquer colaborador — na hora que precisar.",
    highlights: ["SOPs e playbooks", "Metodologias de entrega", "Scripts e templates"],
  },
  {
    icon: HeadphonesIcon,
    tag: "Suporte e atendimento",
    title: "Respostas consistentes em qualquer canal",
    description:
      "Treine o assistente com FAQ, políticas de retorno, contratos e manuais. Seu time responde mais rápido, com mais precisão — sem depender de um sênior para cada situação.",
    highlights: ["FAQ e políticas", "Contratos e SLAs", "Base de erros conhecidos"],
  },
  {
    icon: Users,
    tag: "Operações e times internos",
    title: "Menos interrupções, mais autonomia",
    description:
      "Reduza a dependência de pessoas-chave. Quando alguém precisa de uma informação operacional, o assistente responde antes que o e-mail chegue na caixa de entrada.",
    highlights: ["Políticas internas", "Processos financeiros", "Onboarding de fornecedores"],
  },
  {
    icon: BookOpen,
    tag: "Treinamento e onboarding",
    title: "Novos colaboradores produtivos mais rápido",
    description:
      "Suba o material de treinamento e deixe o novo colaborador interagir diretamente. Menos reuniões de explicação, mais autonomia desde o primeiro dia.",
    highlights: ["Materiais de treinamento", "Cultura e valores", "Fluxos e responsabilidades"],
  },
];

export function UseCasesSection() {
  return (
    <section id="casos-de-uso" className="relative overflow-hidden py-24 sm:py-32">
      {/* Background */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute right-0 top-1/2 h-[500px] w-[500px] -translate-y-1/2 rounded-full bg-[#06b6d4]/6 blur-[120px]" />
      </div>

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mx-auto mb-16 max-w-2xl text-center">
          <p className="mb-3 text-sm font-semibold tracking-widest text-[#06b6d4] uppercase">Casos de uso</p>
          <h2 className="font-display mb-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">
            Feito para equipes que precisam escalar conhecimento
          </h2>
          <p className="text-base text-white/50 sm:text-lg">
            Do time de suporte à agência de performance — o RootFlow adapta ao seu contexto.
          </p>
        </div>

        {/* Use case cards */}
        <div className="grid gap-6 sm:grid-cols-2">
          {USE_CASES.map((uc, i) => (
            <div
              key={i}
              className="group relative overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.02] p-6 transition-all duration-300 hover:border-[#0f63ec]/25 hover:bg-[#0f63ec]/5 sm:p-7"
            >
              <div className="mb-5 flex items-center gap-3">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[#0f63ec]/15">
                  <uc.icon className="h-5 w-5 text-[#3b87f5]" />
                </div>
                <span className="rounded-full border border-[#06b6d4]/25 bg-[#06b6d4]/8 px-3 py-0.5 text-xs font-semibold text-[#06b6d4]">
                  {uc.tag}
                </span>
              </div>

              <h3 className="font-display mb-2 text-lg font-semibold text-white">{uc.title}</h3>
              <p className="mb-5 text-sm leading-relaxed text-white/50">{uc.description}</p>

              <div className="flex flex-wrap gap-2">
                {uc.highlights.map((h, j) => (
                  <span
                    key={j}
                    className="rounded-lg border border-white/[0.07] bg-white/[0.04] px-3 py-1 text-xs text-white/50"
                  >
                    {h}
                  </span>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
