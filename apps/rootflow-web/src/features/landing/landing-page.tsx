import { BenefitsSection } from "@/features/landing/components/benefits-section";
import { SocialProofSection } from "@/features/landing/components/social-proof-section";
import { FaqSection } from "@/features/landing/components/faq-section";
import { FinalCtaSection } from "@/features/landing/components/final-cta-section";
import { HeroSection } from "@/features/landing/components/hero-section";
import { HowItWorksSection } from "@/features/landing/components/how-it-works-section";
import { LandingFooter } from "@/features/landing/components/landing-footer";
import { LandingHeader } from "@/features/landing/components/landing-header";
import { PricingSection } from "@/features/landing/components/pricing-section";
import { ProblemSection } from "@/features/landing/components/problem-section";
import { SolutionSection } from "@/features/landing/components/solution-section";
import { UseCasesSection } from "@/features/landing/components/use-cases-section";
import { WhatsAppButton } from "@/features/landing/components/whatsapp-button";

export function LandingPage() {
  return (
    <div className="min-h-screen bg-[#060d15] font-sans text-white antialiased">
      <LandingHeader />
      <main>
        <HeroSection />
        <ProblemSection />
        <SolutionSection />
        <HowItWorksSection />
        <UseCasesSection />
        <SocialProofSection />
        <BenefitsSection />
        <PricingSection />
        <FaqSection />
        <FinalCtaSection />
      </main>
      <LandingFooter />
      <WhatsAppButton />
    </div>
  );
}
