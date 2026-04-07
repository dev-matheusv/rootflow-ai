import { AlertCircle, Clock, FileQuestion, Repeat, Users } from "lucide-react";

const PAINS = [
  {
    icon: Repeat,
    title: "Mesmas perguntas todos os dias",
    description:
      "Seu time perde horas respondendo as mesmas dúvidas de sempre — sobre processos, clientes, produtos, contratos.",
  },
  {
    icon: FileQuestion,
    title: "Informação espalhada em todo lugar",
    description:
      "Docs no Google Drive, PDFs no e-mail, planilhas no OneDrive, chats no WhatsApp. Ninguém sabe onde encontrar nada.",
  },
  {
    icon: Users,
    title: "Onboarding lento e inconsistente",
    description:
      "Novos colaboradores dependem de alguém sempre disponível para aprender. O conhecimento está nas cabeças, não no sistema.",
  },
  {
    icon: Clock,
    title: "Gargalos nas pessoas certas",
    description:
      "Toda decisão ou dúvida passa pelo mesmo sênior. Ele vira refém das interrupções e nada avança sem ele.",
  },
  {
    icon: AlertCircle,
    title: "Respostas diferentes para a mesma pergunta",
    description:
      "Cada pessoa responde de um jeito. Sem padrão, sem fonte única — e o cliente (ou colaborador) fica confuso.",
  },
];

export function ProblemSection() {
  return (
    <section className="relative overflow-hidden py-24 sm:py-32">
      {/* Background */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute inset-0 bg-gradient-to-b from-[#060d15] via-[#080f1a] to-[#060d15]" />
      </div>

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mx-auto mb-16 max-w-2xl text-center">
          <p className="mb-3 text-sm font-semibold tracking-widest text-[#06b6d4] uppercase">O problema</p>
          <h2 className="font-display mb-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">
            Sua equipe está perdendo tempo{" "}
            <span className="text-white/50">que poderia ir para o que importa</span>
          </h2>
          <p className="text-base text-white/50 sm:text-lg">
            Empresas que crescem acumulam conhecimento espalhado. O resultado é lento, inconsistente e caro.
          </p>
        </div>

        {/* Pain cards */}
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {PAINS.map((pain, i) => (
            <div
              key={i}
              className="group relative overflow-hidden rounded-2xl border border-white/[0.06] bg-white/[0.03] p-6 transition-all duration-300 hover:border-white/10 hover:bg-white/[0.05]"
            >
              <div className="mb-4 flex h-10 w-10 items-center justify-center rounded-xl bg-red-500/10">
                <pain.icon className="h-5 w-5 text-red-400/80" />
              </div>
              <h3 className="mb-2 text-base font-semibold text-white">{pain.title}</h3>
              <p className="text-sm leading-relaxed text-white/50">{pain.description}</p>
            </div>
          ))}

          {/* Closing card */}
          <div className="relative overflow-hidden rounded-2xl border border-[#0f63ec]/20 bg-[#0f63ec]/8 p-6 sm:col-span-2 lg:col-span-1 lg:col-start-3 lg:row-start-2">
            <p className="text-base font-semibold leading-relaxed text-white/80">
              "O conhecimento da empresa existe — ele só não está acessível para quem precisa, na hora certa."
            </p>
            <p className="mt-3 text-sm text-[#06b6d4]">É aí que o RootFlow entra.</p>
          </div>
        </div>
      </div>
    </section>
  );
}
