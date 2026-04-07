import { ChevronDown } from "lucide-react";
import { useState } from "react";

const FAQS = [
  {
    question: "Preciso saber usar IA para configurar?",
    answer:
      "Não. O RootFlow foi desenhado para ser simples. Você sobe os documentos, e a plataforma cuida de todo o processamento de IA. Sem código, sem configuração técnica.",
  },
  {
    question: "Funciona com qualquer tipo de documento?",
    answer:
      "Sim. O RootFlow suporta PDFs, documentos Word, planilhas e outros formatos comuns. Se sua equipe usa, provavelmente já conseguimos processar.",
  },
  {
    question: "É difícil configurar e começar a usar?",
    answer:
      "Não. O setup leva minutos. Você cria a conta, sobe os documentos e começa a perguntar. Sem instalação, sem equipe de TI necessária.",
  },
  {
    question: "Posso testar antes de pagar?",
    answer:
      "Sim. Todos os planos incluem 14 dias de teste gratuito. Sem cartão de crédito exigido. Você só paga se decidir continuar.",
  },
  {
    question: "Meus documentos ficam isolados dos outros clientes?",
    answer:
      "Sim. Cada workspace é completamente isolado. Seu conteúdo é privado e nunca é compartilhado com outros usuários ou usado para treinar modelos externos.",
  },
  {
    question: "Posso cancelar quando quiser?",
    answer:
      "Sim. Não existe fidelidade ou multa. Você cancela pela plataforma a qualquer momento e o acesso é mantido até o fim do período pago.",
  },
  {
    question: "O assistente responde com base nos meus documentos ou usa IA genérica?",
    answer:
      "Exclusivamente com base nos seus documentos. O RootFlow usa RAG (Retrieval-Augmented Generation) para garantir que as respostas são fundamentadas no seu conteúdo — não em informações genéricas da internet.",
  },
];

function FaqItem({ question, answer }: { question: string; answer: string }) {
  const [open, setOpen] = useState(false);

  return (
    <div className="border-b border-white/[0.06] last:border-0">
      <button
        onClick={() => setOpen(!open)}
        className="flex w-full items-center justify-between gap-4 py-5 text-left transition-colors hover:text-white"
        aria-expanded={open}
      >
        <span className={`text-sm font-medium sm:text-base ${open ? "text-white" : "text-white/75"}`}>
          {question}
        </span>
        <ChevronDown
          className={`h-4 w-4 shrink-0 text-white/40 transition-transform duration-200 ${open ? "rotate-180" : ""}`}
        />
      </button>
      {open && (
        <div className="pb-5">
          <p className="text-sm leading-relaxed text-white/50 sm:text-base">{answer}</p>
        </div>
      )}
    </div>
  );
}

export function FaqSection() {
  return (
    <section id="faq" className="relative overflow-hidden py-24 sm:py-32">
      <div className="pointer-events-none absolute inset-0 bg-gradient-to-b from-[#060d15] via-[#07101a] to-[#060d15]" />

      <div className="relative mx-auto max-w-3xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mb-12 text-center">
          <p className="mb-3 text-sm font-semibold tracking-widest text-[#06b6d4] uppercase">Dúvidas frequentes</p>
          <h2 className="font-display text-3xl font-bold tracking-tight text-white sm:text-4xl">
            Perguntas que todo mundo faz
          </h2>
        </div>

        {/* FAQ list */}
        <div className="rounded-2xl border border-white/[0.07] bg-white/[0.02] px-6 sm:px-8">
          {FAQS.map((faq, i) => (
            <FaqItem key={i} question={faq.question} answer={faq.answer} />
          ))}
        </div>
      </div>
    </section>
  );
}
