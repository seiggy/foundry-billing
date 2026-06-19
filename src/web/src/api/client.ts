import type {
  BillingMetric,
  Deployment,
  FoundryAgent,
  FoundryHub,
  FoundryProject,
  PtuCalculationRequest,
  PtuRecommendation,
  SyncHistory,
  SyncStatus,
  TpmAnalytics,
  UsageAnalytics,
  UsageSummary,
} from '../types/billing'

const API_ROOT = '/api'

function toApiPath(path: string) {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`

  return normalizedPath.startsWith(API_ROOT)
    ? normalizedPath
    : `${API_ROOT}${normalizedPath}`
}

export class ApiError extends Error {
  readonly status: number
  readonly details: string

  constructor(status: number, details: string) {
    super(`API request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }
}

async function parseResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T
  }

  const contentType = response.headers.get('content-type') ?? ''

  if (contentType.includes('application/json')) {
    return (await response.json()) as T
  }

  return (await response.text()) as unknown as T
}

export async function apiFetch<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers)

  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json')
  }

  if (init.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(toApiPath(path), {
    ...init,
    headers,
  })

  if (!response.ok) {
    throw new ApiError(response.status, await response.text())
  }

  return parseResponse<T>(response)
}

export const billingClient = {
  getMetrics: () => apiFetch<BillingMetric[]>('/api/billing/metrics'),
  getSummary: () => apiFetch<UsageSummary>('/api/billing/summary'),
  getHubs: () => apiFetch<FoundryHub[]>('/api/hubs'),
  getDeployments: (hubId?: string) =>
    apiFetch<Deployment[]>(
      hubId
        ? `/api/deployments?hubId=${encodeURIComponent(hubId)}`
        : '/api/deployments',
    ),
  getAgents: (hubId?: string, projectId?: string) => {
    const params = new URLSearchParams()

    if (hubId) {
      params.set('hubId', hubId)
    }

    if (projectId) {
      params.set('projectId', projectId)
    }

    const query = params.toString()

    return apiFetch<FoundryAgent[]>(query ? `/api/agents?${query}` : '/api/agents')
  },
  getProjects: () => apiFetch<FoundryProject[]>('/api/projects'),
}

export const analyticsClient = {
  getUsage: (days: number = 30) => apiFetch<UsageAnalytics>(`/api/analytics/usage?days=${days}`),
  getTpm: (days: number = 30) => apiFetch<TpmAnalytics>(`/api/analytics/tpm?days=${days}`),
  calculatePtu: (request: PtuCalculationRequest) =>
    apiFetch<PtuRecommendation>('/api/analytics/ptu-recommendation', {
      method: 'POST',
      body: JSON.stringify(request),
    }),
}

export const syncClient = {
  getStatus: () => apiFetch<SyncStatus>('/api/sync/status'),
  getHistory: () => apiFetch<SyncHistory>('/api/sync/history'),
  trigger: () => apiFetch<{ runId: string }>('/api/sync/trigger', { method: 'POST' }),
}
