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

export interface FoundryAgent {
  id: string
  agentId: string
  name: string
  description: string | null
  modelName: string | null
  kind: 'Prompt' | 'Hosted' | null
  projectName: string
  hubName: string
  createdAt: string | null
  lastSyncedAt: string | null
}

export interface SyncStatus {
  isRunning: boolean
  currentRun: { id: string; startedAt: string; status: string } | null
  lastCompletedAt: string | null
}

export interface SyncRun {
  id: string
  startedAt: string
  completedAt: string | null
  status: 'Running' | 'Completed' | 'Failed'
  errorMessage: string | null
  hubsDiscovered: number
  projectsDiscovered: number
  deploymentsDiscovered: number
  usageSlicesInserted: number
}

export interface SyncHistory {
  runs: SyncRun[]
}
