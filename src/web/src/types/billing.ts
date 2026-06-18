export interface BillingMetric {
  id: string
  projectId: string
  projectName: string
  category: string
  name: string
  value: number
  unit: string
  cost: number
  currency: string
  recordedAt: string
}

export interface FoundryProject {
  id: string
  name: string
  owner: string
  region: string
  environment: string
  status: 'healthy' | 'watch' | 'critical'
  totalCost: number
  currency: string
  lastUpdated: string
}

export interface UsageSummary {
  tenantId: string
  windowStart: string
  windowEnd: string
  totalCost: number
  currency: string
  projectCount: number
  metrics: BillingMetric[]
  projects: FoundryProject[]
}
