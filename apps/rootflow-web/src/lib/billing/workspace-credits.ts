import type { WorkspaceBillingSummary } from "@/lib/api/contracts";

export type WorkspaceCreditTone = "healthy" | "low" | "critical" | "empty" | "inactive";

export interface WorkspaceCreditSnapshot {
  planName: string | null;
  subscriptionStatus: string | null;
  trialEndsAtUtc: string | null;
  trialDaysRemaining: number | null;
  isTrial: boolean;
  isDegraded: boolean;
  availableCredits: number;
  consumedCredits: number;
  totalTrackedCredits: number;
  remainingRatio: number;
  remainingPercent: number;
  tone: WorkspaceCreditTone;
}

export function getWorkspaceCreditSnapshot(summary?: WorkspaceBillingSummary | null): WorkspaceCreditSnapshot | null {
  if (!summary) {
    return null;
  }

  const availableCredits = Math.max(0, summary.balance.availableCredits);
  const consumedCredits = Math.max(0, summary.balance.consumedCredits);
  const includedCredits = Math.max(0, summary.billingPlan?.includedCredits ?? 0);
  const totalTrackedCredits = Math.max(availableCredits + consumedCredits, includedCredits, availableCredits);
  const remainingRatio = totalTrackedCredits > 0 ? clamp(availableCredits / totalTrackedCredits, 0, 1) : 0;
  const remainingPercent = Math.round(remainingRatio * 100);
  const subscriptionStatus = summary.subscriptionStatus ?? summary.subscription?.status ?? null;
  const planName = summary.currentPlanName ?? summary.billingPlan?.name ?? null;
  const trialEndsAtUtc = summary.trialEndsAtUtc ?? summary.subscription?.trialEndsAtUtc ?? null;
  const isTrial = subscriptionStatus === "Trial";
  const isDegraded = Boolean(summary.isDegraded);
  const trialDaysRemaining = isTrial ? getDaysRemaining(trialEndsAtUtc) : null;

  let tone: WorkspaceCreditTone;
  if (subscriptionStatus && subscriptionStatus !== "Active" && subscriptionStatus !== "Trial") {
    tone = "inactive";
  } else if (availableCredits <= 0) {
    tone = "empty";
  } else if (remainingRatio <= 0.15) {
    tone = "critical";
  } else if (remainingRatio <= 0.4) {
    tone = "low";
  } else {
    tone = "healthy";
  }

  return {
    planName,
    subscriptionStatus,
    trialEndsAtUtc,
    trialDaysRemaining,
    isTrial,
    isDegraded,
    availableCredits,
    consumedCredits,
    totalTrackedCredits,
    remainingRatio,
    remainingPercent,
    tone,
  };
}

export function formatCredits(value: number, locale: string) {
  return new Intl.NumberFormat(locale).format(value);
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function getDaysRemaining(input?: string | null) {
  if (!input) {
    return null;
  }

  const trialEnd = new Date(input);
  if (Number.isNaN(trialEnd.getTime())) {
    return null;
  }

  const remainingMs = trialEnd.getTime() - Date.now();
  if (remainingMs <= 0) {
    return 0;
  }

  return Math.ceil(remainingMs / 86_400_000);
}
