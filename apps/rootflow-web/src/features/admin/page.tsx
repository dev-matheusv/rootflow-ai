import { BarChart3, CreditCard, DollarSign, type LucideIcon, Shield, Sparkles } from "lucide-react";
import { Link } from "react-router-dom";

import { useI18n } from "@/app/providers/i18n-provider";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { usePlatformAdminDashboardQuery } from "@/hooks/use-rootflow-data";
import { formatRelativeDate } from "@/lib/formatting/formatters";
import { formatCredits } from "@/lib/billing/workspace-credits";
import { cn } from "@/lib/utils";
import type {
  PlatformAdminBillingTransaction,
  PlatformAdminModelUsage,
  PlatformAdminPaymentIssue,
  PlatformAdminSubscriptionActivity,
  PlatformAdminWorkspaceSummary,
} from "@/lib/api/contracts";

export function AdminPage() {
  const { locale, t } = useI18n();
  const dashboardQuery = usePlatformAdminDashboardQuery();
  const dashboard = dashboardQuery.data;

  const businessMetrics = dashboard
    ? [
        {
          key: "workspaces",
          label: t("admin.totalWorkspaces"),
          value: formatNumber(dashboard.overview.totalWorkspaces, locale),
          icon: Shield,
        },
        {
          key: "subscriptions",
          label: t("admin.activeSubscriptions"),
          value: formatNumber(dashboard.overview.totalActiveSubscriptions, locale),
          icon: CreditCard,
        },
        {
          key: "trials",
          label: t("admin.totalTrials"),
          value: formatNumber(dashboard.overview.totalTrials, locale),
          icon: Sparkles,
        },
        {
          key: "users",
          label: t("admin.totalUsers"),
          value: formatNumber(dashboard.overview.totalUsers, locale),
          icon: BarChart3,
        },
      ]
    : [];

  const usageMetrics = dashboard
    ? [
        {
          key: "availableCredits",
          label: t("admin.availableCredits"),
          value: formatCredits(dashboard.overview.totalAvailableCredits, locale),
          icon: CreditCard,
        },
        {
          key: "consumedCredits",
          label: t("admin.consumedCredits"),
          value: formatCredits(dashboard.overview.totalConsumedCredits, locale),
          icon: CreditCard,
        },
      ]
    : [];

  const financialMetrics = dashboard
    ? [
        {
          key: "providerCost",
          label: t("admin.providerCost"),
          value: formatUsd(dashboard.overview.estimatedProviderCost, locale),
          icon: DollarSign,
        },
        {
          key: "revenueBasis",
          label: t("admin.revenueBasis"),
          value: formatUsd(dashboard.overview.estimatedRevenueBasis, locale),
          icon: DollarSign,
        },
        {
          key: "grossMargin",
          label: t("admin.grossMargin"),
          value: formatUsd(dashboard.overview.estimatedGrossMargin, locale),
          icon: DollarSign,
        },
      ]
    : [];

  return (
    <div className="space-y-5">
      <PageHeader
        eyebrow={t("admin.eyebrow")}
        title={t("admin.title")}
        description={t("admin.description")}
        actions={(
          <>
            <Button variant="outline" asChild>
              <Link to="/billing">{t("common.actions.openBilling")}</Link>
            </Button>
            <Button variant="outline" asChild>
              <Link to="/dashboard">{t("common.actions.backToDashboard")}</Link>
            </Button>
          </>
        )}
      />

      {dashboardQuery.isLoading ? (
        <LoadingState title={t("admin.loadingTitle")} description={t("admin.loadingDescription")} />
      ) : dashboardQuery.isError || !dashboard ? (
        <ErrorState
          title={t("admin.errorTitle")}
          description={t("admin.errorDescription")}
          onRetry={() => dashboardQuery.refetch()}
        />
      ) : (
        <>
          <section className="space-y-3">
            <OverviewMetricSection
              title={t("admin.businessGroup")}
              metrics={businessMetrics}
              gridClassName="sm:grid-cols-2 xl:grid-cols-4"
            />
            <OverviewMetricSection
              title={t("admin.usageGroup")}
              metrics={usageMetrics}
              gridClassName="sm:grid-cols-2"
            />
            <OverviewMetricSection
              title={t("admin.financialGroup")}
              metrics={financialMetrics}
              gridClassName="md:grid-cols-3"
              prominent
            />
          </section>

          <section className="grid gap-3 xl:grid-cols-[1.02fr_0.98fr]">
            <Card className="border-border/80 bg-card/86">
              <CardHeader>
                <div className="space-y-1">
                  <CardTitle>{t("admin.usageWindowsTitle")}</CardTitle>
                  <p className="text-sm text-muted-foreground/95">{t("admin.usageWindowsDescription")}</p>
                </div>
              </CardHeader>
              <CardContent className="grid gap-3 md:grid-cols-3">
                {dashboard.usageWindows.map((window) => (
                  <div key={window.key} className="rounded-[22px] border border-border/78 bg-background/84 p-4">
                    <div className="flex items-center justify-between gap-3">
                      <div className="text-sm font-semibold text-foreground">{t(`admin.window.${window.key}`)}</div>
                      <Badge variant="secondary">{formatNumber(window.eventCount, locale)} {t("admin.eventsShort")}</Badge>
                    </div>
                    <div className="mt-3 space-y-2 text-sm">
                      <MetricRow label={t("admin.tokens")} value={formatNumber(window.totalTokens, locale)} />
                      <MetricRow label={t("admin.providerCost")} value={formatUsd(window.estimatedProviderCost, locale)} />
                      <MetricRow label={t("admin.revenueBasis")} value={formatUsd(window.estimatedRevenueBasis, locale)} />
                      <MetricRow label={t("admin.grossMargin")} value={formatUsd(window.estimatedGrossMargin, locale)} />
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>

            <Card className="border-border/80 bg-card/86">
              <CardHeader>
                <div className="space-y-1">
                  <CardTitle>{t("admin.alertsTitle")}</CardTitle>
                  <p className="text-sm text-muted-foreground/95">{t("admin.alertsDescription")}</p>
                </div>
              </CardHeader>
              <CardContent className="grid gap-3 sm:grid-cols-2">
                <AlertCard title={t("admin.lowCreditAlert")} count={dashboard.alerts.lowCreditWorkspaces} tone="warning" />
                <AlertCard title={t("admin.noCreditAlert")} count={dashboard.alerts.noCreditWorkspaces} tone="critical" />
                <AlertCard title={t("admin.expiringTrialAlert")} count={dashboard.alerts.trialsExpiringSoon} tone="info" />
                <AlertCard title={t("admin.paymentIssueAlert")} count={dashboard.alerts.paymentIssues} tone="critical" />
              </CardContent>
            </Card>
          </section>

          <section className="grid gap-3 xl:grid-cols-[1.05fr_0.95fr]">
            <div className="space-y-3">
              <WorkspaceListCard
                title={t("admin.lowCreditWorkspacesTitle")}
                description={t("admin.lowCreditWorkspacesDescription")}
                emptyMessage={t("admin.noLowCreditWorkspaces")}
                locale={locale}
                items={dashboard.lowCreditWorkspaces}
                t={t}
              />
              <WorkspaceListCard
                title={t("admin.noCreditWorkspacesTitle")}
                description={t("admin.noCreditWorkspacesDescription")}
                emptyMessage={t("admin.noEmptyWorkspaces")}
                locale={locale}
                items={dashboard.noCreditWorkspaces}
                t={t}
                showEmptyAsCritical
              />
              <WorkspaceListCard
                title={t("admin.trialsExpiringTitle")}
                description={t("admin.trialsExpiringDescription")}
                emptyMessage={t("admin.noTrialsExpiring")}
                locale={locale}
                items={dashboard.trialsExpiringSoon}
                t={t}
                emphasizeTrialDate
              />
              <PaymentIssuesCard items={dashboard.paymentIssues} locale={locale} t={t} />
            </div>

            <div className="space-y-3">
              <BillingActivityCard title={t("admin.creditPurchasesTitle")} items={dashboard.recentCreditPurchases} locale={locale} t={t} />
              <SubscriptionActivityCard items={dashboard.recentSubscriptionChanges} locale={locale} t={t} />
              <ModelBreakdownCard items={dashboard.modelBreakdown} locale={locale} t={t} />
            </div>
          </section>

          <section className="grid gap-3 xl:grid-cols-3">
            <WorkspaceLeaderboardCard
              title={t("admin.topCreditConsumersTitle")}
              emptyMessage={t("admin.noUsageData")}
              items={dashboard.topCreditConsumers}
              locale={locale}
              metricLabel={t("admin.creditsCharged")}
              metricValue={(workspace) => formatCredits(workspace.creditsCharged, locale)}
              t={t}
            />
            <WorkspaceLeaderboardCard
              title={t("admin.topProviderCostTitle")}
              emptyMessage={t("admin.noUsageData")}
              items={dashboard.topProviderCostWorkspaces}
              locale={locale}
              metricLabel={t("admin.providerCost")}
              metricValue={(workspace) => formatUsd(workspace.estimatedProviderCost, locale)}
              t={t}
            />
            <WorkspaceLeaderboardCard
              title={t("admin.topRevenueBasisTitle")}
              emptyMessage={t("admin.noUsageData")}
              items={dashboard.topRevenueBasisWorkspaces}
              locale={locale}
              metricLabel={t("admin.revenueBasis")}
              metricValue={(workspace) => formatUsd(workspace.estimatedRevenueBasis, locale)}
              t={t}
            />
          </section>
        </>
      )}
    </div>
  );
}

function OverviewMetricSection({
  title,
  metrics,
  gridClassName,
  prominent = false,
}: {
  title: string;
  metrics: {
    key: string;
    label: string;
    value: string;
    icon: LucideIcon;
  }[];
  gridClassName: string;
  prominent?: boolean;
}) {
  if (metrics.length === 0) {
    return null;
  }

  return (
    <div className="space-y-2">
      <div className="px-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
        {title}
      </div>
      <div className={cn("grid gap-2.5", gridClassName)}>
        {metrics.map((metric) => (
          <CompactMetricCard
            key={metric.key}
            label={metric.label}
            value={metric.value}
            icon={metric.icon}
            prominent={prominent}
          />
        ))}
      </div>
    </div>
  );
}

function CompactMetricCard({
  label,
  value,
  icon: Icon,
  prominent = false,
}: {
  label: string;
  value: string;
  icon: LucideIcon;
  prominent?: boolean;
}) {
  return (
    <Card className={cn(
      "border bg-card/86",
      prominent ? "border-primary/16 bg-primary/[0.05]" : "border-border/80",
    )}>
      <CardContent className="p-3.5">
        <div className="flex items-center gap-3">
          <div
            className={cn(
              "flex size-8 shrink-0 items-center justify-center rounded-[16px] border",
              prominent
                ? "border-primary/18 bg-background/80 text-primary"
                : "border-primary/14 bg-primary/10 text-primary",
            )}
          >
            <Icon className="size-4" />
          </div>
          <div className="min-w-0">
            <div className={cn(
              "truncate text-[12px] font-medium",
              prominent ? "text-foreground/88" : "text-muted-foreground",
            )}>
              {label}
            </div>
            <div
              className={cn(
                "mt-0.5 font-display font-semibold tracking-[-0.05em] text-foreground",
                prominent ? "text-[1.6rem]" : "text-[1.35rem]",
              )}
            >
              {value}
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function MetricRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-right font-medium text-foreground">{value}</span>
    </div>
  );
}

function AlertCard({
  title,
  count,
  tone,
}: {
  title: string;
  count: number;
  tone: "warning" | "critical" | "info";
}) {
  const badgeVariant = tone === "warning" ? "warning" : tone === "critical" ? "default" : "secondary";
  const containerClassName =
    tone === "warning"
      ? "border-amber-500/20 bg-amber-500/[0.08]"
      : tone === "critical"
        ? "border-rose-500/22 bg-rose-500/[0.1]"
        : "border-border/78 bg-background/82";

  return (
    <div className={`rounded-[22px] border p-4 ${containerClassName}`}>
      <div className="flex items-center justify-between gap-3">
        <div className="text-sm font-semibold text-foreground">{title}</div>
        <Badge variant={badgeVariant}>{count}</Badge>
      </div>
    </div>
  );
}

function WorkspaceListCard({
  title,
  description,
  emptyMessage,
  items,
  locale,
  t,
  emphasizeTrialDate = false,
  showEmptyAsCritical = false,
}: {
  title: string;
  description: string;
  emptyMessage: string;
  items: PlatformAdminWorkspaceSummary[];
  locale: string;
  t: (key: string, values?: Record<string, string | number>) => string;
  emphasizeTrialDate?: boolean;
  showEmptyAsCritical?: boolean;
}) {
  return (
    <Card className="border-border/80 bg-card/86">
      <CardHeader>
        <div className="space-y-1">
          <CardTitle>{title}</CardTitle>
          <p className="text-sm text-muted-foreground/95">{description}</p>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {items.length === 0 ? (
          <div className="rounded-[18px] border border-dashed border-border/82 bg-background/72 px-4 py-3 text-sm text-muted-foreground">
            {emptyMessage}
          </div>
        ) : (
          items.map((workspace) => (
            <div key={workspace.workspaceId} className="rounded-[20px] border border-border/78 bg-background/82 p-3.5">
              <div className="flex min-w-0 items-start justify-between gap-3">
                <div className="min-w-0 space-y-1">
                  <div className="truncate text-sm font-semibold text-foreground">{workspace.workspaceName}</div>
                  <div className="truncate text-xs text-muted-foreground">@{workspace.workspaceSlug}</div>
                </div>
                <Badge variant={showEmptyAsCritical && workspace.availableCredits <= 0 ? "default" : "secondary"}>
                  {workspace.planName ?? workspace.subscriptionStatus}
                </Badge>
              </div>
              <div className="mt-3 grid gap-2 text-sm">
                <MetricRow label={t("billing.available")} value={formatCredits(workspace.availableCredits, locale)} />
                <MetricRow label={t("admin.remainingLabel")} value={`${workspace.remainingPercent.toFixed(1)}%`} />
                {emphasizeTrialDate ? (
                  <MetricRow
                    label={t("admin.trialEndsLabel")}
                    value={workspace.trialEndsAtUtc ? formatRelativeDate(workspace.trialEndsAtUtc, locale as "en" | "pt-BR") : "-"}
                  />
                ) : (
                  <MetricRow
                    label={t("admin.lastUsageLabel")}
                    value={workspace.lastUsageAtUtc ? formatRelativeDate(workspace.lastUsageAtUtc, locale as "en" | "pt-BR") : "-"}
                  />
                )}
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}

function PaymentIssuesCard({
  items,
  locale,
  t,
}: {
  items: PlatformAdminPaymentIssue[];
  locale: string;
  t: (key: string, values?: Record<string, string | number>) => string;
}) {
  return (
    <Card className="border-border/80 bg-card/86">
      <CardHeader>
        <div className="space-y-1">
          <CardTitle>{t("admin.paymentWatchTitle")}</CardTitle>
          <p className="text-sm text-muted-foreground/95">{t("admin.paymentWatchDescription")}</p>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {items.length === 0 ? (
          <div className="rounded-[18px] border border-dashed border-border/82 bg-background/72 px-4 py-3 text-sm text-muted-foreground">
            {t("admin.noPaymentIssues")}
          </div>
        ) : (
          items.map((issue) => (
            <div key={issue.transactionId} className="rounded-[20px] border border-border/78 bg-background/82 p-3.5">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-foreground">{issue.workspaceName}</div>
                  <div className="truncate text-xs text-muted-foreground">@{issue.workspaceSlug}</div>
                </div>
                <Badge variant="warning">{issue.status}</Badge>
              </div>
              <div className="mt-3 grid gap-2 text-sm">
                <MetricRow label={t("common.labels.type")} value={issue.type} />
                <MetricRow label={t("admin.amountLabel")} value={formatCurrency(issue.amount, issue.currencyCode, locale)} />
                <MetricRow label={t("common.labels.updatedLabel")} value={formatRelativeDate(issue.updatedAtUtc, locale as "en" | "pt-BR")} />
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}

function BillingActivityCard({
  title,
  items,
  locale,
  t,
}: {
  title: string;
  items: PlatformAdminBillingTransaction[];
  locale: string;
  t: (key: string, values?: Record<string, string | number>) => string;
}) {
  return (
    <Card className="border-border/80 bg-card/86">
      <CardHeader>
        <div className="space-y-1">
          <CardTitle>{title}</CardTitle>
          <p className="text-sm text-muted-foreground/95">{t("admin.recentCreditPurchasesDescription")}</p>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {items.length === 0 ? (
          <div className="rounded-[18px] border border-dashed border-border/82 bg-background/72 px-4 py-3 text-sm text-muted-foreground">
            {t("admin.noCreditPurchases")}
          </div>
        ) : (
          items.map((item) => (
            <div key={item.transactionId} className="rounded-[20px] border border-border/78 bg-background/82 p-3.5">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-foreground">{item.workspaceName}</div>
                  <div className="truncate text-xs text-muted-foreground">@{item.workspaceSlug}</div>
                </div>
                <Badge variant={item.status === "Completed" ? "success" : "secondary"}>{item.status}</Badge>
              </div>
              <div className="mt-3 grid gap-2 text-sm">
                <MetricRow label={t("admin.creditsCharged")} value={item.credits ? formatCredits(item.credits, locale) : "-"} />
                <MetricRow label={t("admin.amountLabel")} value={formatCurrency(item.amount, item.currencyCode, locale)} />
                <MetricRow label={t("common.labels.updatedLabel")} value={formatRelativeDate(item.occurredAtUtc, locale as "en" | "pt-BR")} />
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}

function SubscriptionActivityCard({
  items,
  locale,
  t,
}: {
  items: PlatformAdminSubscriptionActivity[];
  locale: string;
  t: (key: string, values?: Record<string, string | number>) => string;
}) {
  return (
    <Card className="border-border/80 bg-card/86">
      <CardHeader>
        <div className="space-y-1">
          <CardTitle>{t("admin.subscriptionsTitle")}</CardTitle>
          <p className="text-sm text-muted-foreground/95">{t("admin.subscriptionsDescription")}</p>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {items.length === 0 ? (
          <div className="rounded-[18px] border border-dashed border-border/82 bg-background/72 px-4 py-3 text-sm text-muted-foreground">
            {t("admin.noSubscriptionChanges")}
          </div>
        ) : (
          items.map((item) => (
            <div key={`${item.workspaceId}-${item.updatedAtUtc}`} className="rounded-[20px] border border-border/78 bg-background/82 p-3.5">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-foreground">{item.workspaceName}</div>
                  <div className="truncate text-xs text-muted-foreground">@{item.workspaceSlug}</div>
                </div>
                <Badge variant={item.status === "Active" ? "success" : item.status === "Trial" ? "warning" : "secondary"}>
                  {item.status}
                </Badge>
              </div>
              <div className="mt-3 grid gap-2 text-sm">
                <MetricRow label={t("billing.currentPlan")} value={item.planName ?? "-"} />
                <MetricRow label={t("common.labels.updatedLabel")} value={formatRelativeDate(item.updatedAtUtc, locale as "en" | "pt-BR")} />
                <MetricRow
                  label={item.status === "Trial" ? t("admin.trialEndsLabel") : t("admin.periodEndLabel")}
                  value={formatRelativeDate(item.status === "Trial" && item.trialEndsAtUtc ? item.trialEndsAtUtc : item.currentPeriodEndUtc, locale as "en" | "pt-BR")}
                />
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}

function ModelBreakdownCard({
  items,
  locale,
  t,
}: {
  items: PlatformAdminModelUsage[];
  locale: string;
  t: (key: string, values?: Record<string, string | number>) => string;
}) {
  return (
    <Card className="border-border/80 bg-card/86">
      <CardHeader>
        <div className="space-y-1">
          <CardTitle>{t("admin.modelUsageTitle")}</CardTitle>
          <p className="text-sm text-muted-foreground/95">{t("admin.modelUsageDescription")}</p>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {items.length === 0 ? (
          <div className="rounded-[18px] border border-dashed border-border/82 bg-background/72 px-4 py-3 text-sm text-muted-foreground">
            {t("admin.noModelUsage")}
          </div>
        ) : (
          items.map((item) => (
            <div key={`${item.provider}-${item.model}`} className="rounded-[20px] border border-border/78 bg-background/82 p-3.5">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-foreground">{item.model}</div>
                  <div className="truncate text-xs text-muted-foreground">{item.provider}</div>
                </div>
                <Badge variant="secondary">{formatNumber(item.eventCount, locale)} {t("admin.eventsShort")}</Badge>
              </div>
              <div className="mt-3 grid gap-2 text-sm">
                <MetricRow label={t("admin.tokens")} value={formatNumber(item.totalTokens, locale)} />
                <MetricRow label={t("admin.providerCost")} value={formatUsd(item.estimatedProviderCost, locale)} />
                <MetricRow label={t("admin.revenueBasis")} value={formatUsd(item.estimatedRevenueBasis, locale)} />
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}

function WorkspaceLeaderboardCard({
  title,
  emptyMessage,
  items,
  locale,
  metricLabel,
  metricValue,
  t,
}: {
  title: string;
  emptyMessage: string;
  items: PlatformAdminWorkspaceSummary[];
  locale: string;
  metricLabel: string;
  metricValue: (workspace: PlatformAdminWorkspaceSummary) => string;
  t: (key: string, values?: Record<string, string | number>) => string;
}) {
  return (
    <Card className="border-border/80 bg-card/86">
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {items.length === 0 ? (
          <div className="rounded-[18px] border border-dashed border-border/82 bg-background/72 px-4 py-3 text-sm text-muted-foreground">
            {emptyMessage}
          </div>
        ) : (
          items.map((workspace, index) => (
            <div key={workspace.workspaceId} className="rounded-[20px] border border-border/78 bg-background/82 p-3.5">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-foreground">
                    {index + 1}. {workspace.workspaceName}
                  </div>
                  <div className="truncate text-xs text-muted-foreground">@{workspace.workspaceSlug}</div>
                </div>
                <Badge variant="secondary">{workspace.planName ?? workspace.subscriptionStatus}</Badge>
              </div>
              <div className="mt-3 grid gap-2 text-sm">
                <MetricRow label={metricLabel} value={metricValue(workspace)} />
                <MetricRow label={t("common.labels.members")} value={formatNumber(workspace.memberCount, locale)} />
                <MetricRow label={t("admin.lastUsageLabel")} value={workspace.lastUsageAtUtc ? formatRelativeDate(workspace.lastUsageAtUtc, locale as "en" | "pt-BR") : "-"} />
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}

function formatNumber(value: number, locale: string) {
  return new Intl.NumberFormat(locale).format(value);
}

function formatUsd(value: number, locale: string) {
  return formatCurrency(value, "USD", locale);
}

function formatCurrency(value: number, currencyCode: string, locale: string) {
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency: currencyCode,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}
