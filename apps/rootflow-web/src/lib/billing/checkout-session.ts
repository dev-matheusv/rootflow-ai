export type BillingCheckoutKind = "plan" | "credits";

export interface BillingCheckoutContext {
  workspaceId: string;
  sessionId: string;
  kind: BillingCheckoutKind;
  targetCode: string;
  baselinePlanCode: string | null;
  baselineAvailableCredits: number | null;
  baselineBalanceUpdatedAtUtc: string | null;
  createdAtUtc: string;
}

const BILLING_CHECKOUT_STORAGE_KEY = "rootflow.billing.checkout-context";

export function storeBillingCheckoutContext(context: BillingCheckoutContext) {
  if (typeof window === "undefined") {
    return;
  }

  window.sessionStorage.setItem(BILLING_CHECKOUT_STORAGE_KEY, JSON.stringify(context));
}

export function readBillingCheckoutContext(): BillingCheckoutContext | null {
  if (typeof window === "undefined") {
    return null;
  }

  const rawValue = window.sessionStorage.getItem(BILLING_CHECKOUT_STORAGE_KEY);
  if (!rawValue) {
    return null;
  }

  try {
    return JSON.parse(rawValue) as BillingCheckoutContext;
  } catch {
    window.sessionStorage.removeItem(BILLING_CHECKOUT_STORAGE_KEY);
    return null;
  }
}

export function clearBillingCheckoutContext() {
  if (typeof window === "undefined") {
    return;
  }

  window.sessionStorage.removeItem(BILLING_CHECKOUT_STORAGE_KEY);
}
