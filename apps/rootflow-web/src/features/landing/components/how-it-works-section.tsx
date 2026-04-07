import { ArrowRight, Bot, Brain, Upload } from "lucide-react";

const STEPS = [
  {
    number: "01",
    icon: Upload,
    title: "Suba seus documentos",
    description:
      "Importe PDFs, DOCs, planilhas e manuais da sua empresa. O processo leva minutos, não dias.",
    color: "#0f63ec",
  },
  {
    number: "02",
    icon: Brain,
    title: "A IA processa e aprende",
    description:
      "O RootFlow organiza e indexa todo o conteúdo com embeddings de IA. Seu conhecimento se torna pesquisável e inteligente.",
    color: "#3b87f5",
  },
  {
    number: "03",
    icon: Bot,
    title: "Pergunte e receba respostas precisas",
    description:
      "Qualquer membro da equipe faz perguntas em linguagem natural e recebe respostas fundamentadas no seu conteúdo — com a fonte indicada.",
    color: "#06b6d4",
  },
];

export function HowItWorksSection() {
  return (
    <section id="como-funciona" className="relative overflow-hidden py-24 sm:py-32">
      {/* Background */}
      <div className="pointer-events-none absolute inset-0 bg-gradient-to-b from-[#060d15] via-[#07101a] to-[#060d15]" />

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mx-auto mb-16 max-w-2xl text-center">
          <p className="mb-3 text-sm font-semibold tracking-widest text-[#06b6d4] uppercase">Como funciona</p>
          <h2 className="font-display mb-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">
            Do documento à resposta em 3 passos
          </h2>
          <p className="text-base text-white/50 sm:text-lg">
            Sem configuração complexa. Sem equipe técnica. Só resultado.
          </p>
        </div>

        {/* Steps */}
        <div className="relative">
          {/* Connector line (desktop) */}
          <div className="absolute top-10 left-0 right-0 hidden h-px bg-gradient-to-r from-transparent via-white/10 to-transparent lg:block" />

          <div className="grid gap-8 lg:grid-cols-3">
            {STEPS.map((step, i) => (
              <div key={i} className="relative flex flex-col items-center text-center">
                {/* Arrow between steps */}
                {i < STEPS.length - 1 && (
                  <ArrowRight className="absolute top-8 -right-4 z-10 hidden h-5 w-5 text-white/20 lg:block" />
                )}

                {/* Icon circle */}
                <div
                  className="relative mb-6 flex h-20 w-20 items-center justify-center rounded-2xl border border-white/10"
                  style={{
                    background: `radial-gradient(circle at center, ${step.color}22 0%, ${step.color}08 100%)`,
                    boxShadow: `0 0 40px ${step.color}22`,
                  }}
                >
                  <step.icon className="h-8 w-8" style={{ color: step.color }} />
                  <span
                    className="absolute -top-2.5 -right-2.5 flex h-6 w-6 items-center justify-center rounded-full border border-white/10 bg-[#0d1520] text-xs font-bold"
                    style={{ color: step.color }}
                  >
                    {step.number.slice(-1)}
                  </span>
                </div>

                <h3 className="font-display mb-3 text-lg font-semibold text-white">{step.title}</h3>
                <p className="max-w-xs text-sm leading-relaxed text-white/50">{step.description}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
