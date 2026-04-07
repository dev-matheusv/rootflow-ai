import { Bot, Brain, FileText, Search } from "lucide-react";
import { Link } from "react-router-dom";

const FEATURES = [
  {
    icon: FileText,
    title: "Suba qualquer documento",
    description: "PDFs, DOCs, planilhas, manuais. O RootFlow processa e organiza tudo automaticamente.",
  },
  {
    icon: Brain,
    title: "IA aprende com seu conteúdo",
    description: "Seu assistente é treinado exclusivamente no conhecimento da sua empresa — sem misturar com outras.",
  },
  {
    icon: Search,
    title: "Respostas com fonte",
    description: "Cada resposta indica exatamente de onde veio a informação. Nada de achismo — só o que está no seu conteúdo.",
  },
  {
    icon: Bot,
    title: "Disponível para toda a equipe",
    description: "Qualquer colaborador pergunta, o assistente responde. Em segundos, a qualquer hora.",
  },
];

export function SolutionSection() {
  return (
    <section className="relative overflow-hidden py-24 sm:py-32">
      {/* Background */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute top-1/2 left-1/2 h-[700px] w-[700px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-[#0f63ec]/8 blur-[140px]" />
      </div>

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="grid items-center gap-16 lg:grid-cols-2">
          {/* Left: copy */}
          <div>
            <p className="mb-3 text-sm font-semibold tracking-widest text-[#06b6d4] uppercase">A solução</p>
            <h2 className="font-display mb-6 text-3xl font-bold tracking-tight text-white sm:text-4xl lg:text-5xl">
              Um assistente de IA treinado{" "}
              <span className="bg-gradient-to-r from-[#0f63ec] to-[#06b6d4] bg-clip-text text-transparent">
                no conhecimento da sua empresa
              </span>
            </h2>
            <p className="mb-8 text-base leading-relaxed text-white/55 sm:text-lg">
              O RootFlow transforma seus documentos internos em uma base de conhecimento inteligente. Qualquer
              colaborador encontra respostas precisas em segundos — sem depender de ninguém.
            </p>
            <Link
              to="/auth/signup"
              className="inline-flex items-center gap-2 rounded-2xl bg-[#0f63ec] px-6 py-3 text-sm font-semibold text-white shadow-[0_0_32px_rgba(15,99,236,0.35)] transition-all hover:-translate-y-0.5 hover:shadow-[0_0_48px_rgba(15,99,236,0.5)]"
            >
              Experimentar grátis
            </Link>
          </div>

          {/* Right: feature grid */}
          <div className="grid gap-4 sm:grid-cols-2">
            {FEATURES.map((feature, i) => (
              <div
                key={i}
                className="rounded-2xl border border-white/[0.07] bg-white/[0.03] p-5 transition-all hover:border-[#0f63ec]/25 hover:bg-[#0f63ec]/5"
              >
                <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-[#0f63ec]/15">
                  <feature.icon className="h-5 w-5 text-[#3b87f5]" />
                </div>
                <h3 className="mb-1.5 text-sm font-semibold text-white">{feature.title}</h3>
                <p className="text-xs leading-relaxed text-white/50">{feature.description}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
