import { useCallback } from 'react'
import { billingClient } from '../api/client'
import { Panel } from '../components/Panel'
import { useApi } from '../hooks/useApi'

const compactNumberFormatter = new Intl.NumberFormat('en-US', {
  notation: 'compact',
  maximumFractionDigits: 1,
})

const numberFormatter = new Intl.NumberFormat('en-US')

const dateTimeFormatter = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
})

function formatTokenCount(value: number) {
  return `${compactNumberFormatter.format(value)} tokens`
}

function formatExactTokens(value: number) {
  return `${numberFormatter.format(value)} tokens`
}

function formatDateTime(value: string | null) {
  if (!value) {
    return 'No data yet'
  }

  return dateTimeFormatter.format(new Date(value))
}

function formatModelLabel(modelName: string, modelVersion: string | null) {
  return modelVersion ? `${modelName} · ${modelVersion}` : modelName
}

export function Dashboard() {
  const summaryRequest = useCallback(() => billingClient.getSummary(), [])
  const metricsRequest = useCallback(() => billingClient.getMetrics(), [])

  const {
    data: summary,
    loading: summaryLoading,
    error: summaryError,
    refresh: refreshSummary,
  } = useApi(summaryRequest)
  const {
    data: metrics,
    loading: metricsLoading,
    error: metricsError,
    refresh: refreshMetrics,
  } = useApi(metricsRequest)

  const refreshAll = () => {
    refreshSummary()
    refreshMetrics()
  }

  const recentMetrics = [...(metrics ?? [])]
    .sort((left, right) => Date.parse(right.timestamp) - Date.parse(left.timestamp))
    .slice(0, 8)
  const models = [...(summary?.byModel ?? [])].sort(
    (left, right) => right.totalTokens - left.totalTokens,
  )
  const errorMessage = summaryError ?? metricsError
  const isLoading = (summaryLoading && !summary) || (metricsLoading && !metrics)

  if (isLoading) {
    return (
      <section className="page">
        <header className="page-header">
          <div>
            <p className="page-eyebrow">Dashboard</p>
            <h2 className="page-title">Live token activity</h2>
          </div>
          <p className="page-note">Loading usage summary and metric stream…</p>
        </header>
      </section>
    )
  }

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="page-eyebrow">Dashboard</p>
          <h2 className="page-title">Live token activity</h2>
        </div>
        <p className="page-note">
          Window start: {formatDateTime(summary?.oldestMetric ?? null)}
          <br />
          Latest metric: {formatDateTime(summary?.newestMetric ?? null)}
        </p>
      </header>

      {errorMessage ? (
        <Panel
          title="Live feed issue"
          subtitle={errorMessage}
          aside={
            <button type="button" className="action-button" onClick={refreshAll}>
              Retry
            </button>
          }
        >
          <div className="empty-state">
            <p>Showing the last successful payload where possible.</p>
          </div>
        </Panel>
      ) : null}

      <section className="summary-grid" aria-label="Usage summary">
        <article className="summary-card">
          <span className="summary-card-label">Total tokens</span>
          <strong className="summary-card-value">
            {formatTokenCount(summary?.totalTokens ?? 0)}
          </strong>
          <p className="summary-card-detail">{formatExactTokens(summary?.totalTokens ?? 0)}</p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Prompt / completion</span>
          <strong className="summary-card-value">
            {compactNumberFormatter.format(summary?.totalPromptTokens ?? 0)}
          </strong>
          <p className="summary-card-detail">
            Prompt tokens
            <br />
            {formatExactTokens(summary?.totalCompletionTokens ?? 0)} completion
          </p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Observed surface</span>
          <strong className="summary-card-value">{summary?.deploymentCount ?? 0}</strong>
          <p className="summary-card-detail">
            deployments across {summary?.hubCount ?? 0} hubs and {summary?.projectCount ?? 0}{' '}
            projects
          </p>
        </article>
      </section>

      <div className="page-grid">
        <Panel
          title="Model breakdown"
          subtitle="Sorted by total token volume across the captured window."
          aside={
            <button type="button" className="action-button" onClick={refreshAll}>
              Refresh
            </button>
          }
        >
          {models.length === 0 ? (
            <div className="empty-state">
              <p>No model usage has been reported yet.</p>
            </div>
          ) : (
            <ul className="project-list">
              {models.map((model) => (
                <li key={model.modelName} className="project-item">
                  <div className="item-headline">
                    <span className="item-title">{model.modelName}</span>
                    <span className="item-value">{formatTokenCount(model.totalTokens)}</span>
                  </div>
                  <div className="item-meta">
                    <span>Prompt: {formatExactTokens(model.promptTokens)}</span>
                    <span>Completion: {formatExactTokens(model.completionTokens)}</span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Panel>

        <Panel
          title="Deployment activity"
          subtitle="Most recent usage slices from the billing metrics feed."
        >
          {recentMetrics.length === 0 ? (
            <div className="empty-state">
              <p>No deployment activity has been recorded yet.</p>
            </div>
          ) : (
            <ul className="metric-list">
              {recentMetrics.map((metric) => (
                <li
                  key={`${metric.deploymentName}-${metric.timestamp}-${metric.modelName}`}
                  className="metric-item"
                >
                  <div className="item-headline">
                    <span className="item-title">{metric.deploymentName}</span>
                    <span className="item-value">{formatTokenCount(metric.totalTokens)}</span>
                  </div>
                  <div className="item-meta">
                    <span>{formatModelLabel(metric.modelName, metric.modelVersion)}</span>
                    <span>{metric.hubName}</span>
                    <span>{formatDateTime(metric.timestamp)}</span>
                  </div>
                  <p className="item-note">
                    Prompt {formatExactTokens(metric.promptTokens)} · Completion{' '}
                    {formatExactTokens(metric.completionTokens)}
                  </p>
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </section>
  )
}
