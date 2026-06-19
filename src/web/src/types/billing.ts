export interface BillingMetric {
  deploymentName: string
  modelName: string
  modelVersion: string | null
  hubName: string
  timestamp: string
  promptTokens: number
  completionTokens: number
  totalTokens: number
}

export interface UsageSummary {
  hubCount: number
  projectCount: number
  deploymentCount: number
  totalPromptTokens: number
  totalCompletionTokens: number
  totalTokens: number
  oldestMetric: string | null
  newestMetric: string | null
  byModel: ModelUsageBreakdown[]
}

export interface ModelUsageBreakdown {
  modelName: string
  promptTokens: number
  completionTokens: number
  totalTokens: number
}

export interface FoundryHub {
  id: string
  name: string
  region: string
  subscriptionId: string
  deploymentCount: number
  projectCount: number
  lastSyncedAt: string | null
}

export interface Deployment {
  id: string
  deploymentName: string
  modelName: string
  modelVersion: string | null
  hubName: string
  totalTokensLast24h: number
  lastMetricAt: string | null
}

export interface FoundryProject {
  id: string
  name: string
  hubName: string
  region: string
  lastSyncedAt: string | null
}
