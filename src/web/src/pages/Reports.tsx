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
    return 'No metric yet'
  }

  return dateTimeFormatter.format(new Date(value))
}

function formatModelLabel(modelName: string, modelVersion: string | null) {
  return modelVersion ? `${modelName} · ${modelVersion}` : modelName
}

export function Reports() {
  const deploymentsRequest = useCallback(() => billingClient.getDeployments(), [])
  const {
    data: deployments,
    loading,
    error,
    refresh,
  } = useApi(deploymentsRequest)

  const sortedDeployments = [...(deployments ?? [])].sort(
    (left, right) => right.totalTokensLast24h - left.totalTokensLast24h,
  )
  const totalTokens = sortedDeployments.reduce(
    (sum, deployment) => sum + deployment.totalTokensLast24h,
    0,
  )
  const topDeployment = sortedDeployments[0] ?? null
  const modelTotals = Array.from(
    sortedDeployments.reduce((groups, deployment) => {
      const currentTotal = groups.get(deployment.modelName) ?? 0
      groups.set(deployment.modelName, currentTotal + deployment.totalTokensLast24h)
      return groups
    }, new Map<string, number>()),
  ).sort((left, right) => right[1] - left[1])

  if (loading && !deployments) {
    return (
      <section className="page">
        <header className="page-header">
          <div>
            <p className="page-eyebrow">Reports</p>
            <h2 className="page-title">Deployment usage</h2>
          </div>
          <p className="page-note">Loading 24-hour deployment activity…</p>
        </header>
      </section>
    )
  }

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="page-eyebrow">Reports</p>
          <h2 className="page-title">Deployment usage</h2>
        </div>
        <p className="page-note">
          {sortedDeployments.length} deployments
          <br />
          Rolling 24-hour token view
        </p>
      </header>

      {error ? (
        <Panel
          title="Deployment feed issue"
          subtitle={error}
          aside={
            <button type="button" className="action-button" onClick={refresh}>
              Retry
            </button>
          }
        >
          <div className="empty-state">
            <p>Reports will refresh automatically after the next successful request.</p>
          </div>
        </Panel>
      ) : null}

      <section className="summary-grid" aria-label="Deployment usage summary">
        <article className="summary-card">
          <span className="summary-card-label">Deployments</span>
          <strong className="summary-card-value">{sortedDeployments.length}</strong>
          <p className="summary-card-detail">Items reporting usage in the last 24 hours.</p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">24h token volume</span>
          <strong className="summary-card-value">{formatTokenCount(totalTokens)}</strong>
          <p className="summary-card-detail">{formatExactTokens(totalTokens)}</p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Top deployment</span>
          <strong className="summary-card-value">
            {topDeployment ? formatTokenCount(topDeployment.totalTokensLast24h) : '0 tokens'}
          </strong>
          <p className="summary-card-detail">
            {topDeployment
              ? `${topDeployment.deploymentName} · ${topDeployment.hubName}`
              : 'No deployment activity yet.'}
          </p>
        </article>
      </section>

      <div className="page-grid">
        <Panel
          title="Deployment usage"
          subtitle="Sorted by total token activity in the last 24 hours."
          aside={
            <button type="button" className="action-button" onClick={refresh}>
              Refresh
            </button>
          }
        >
          {sortedDeployments.length === 0 ? (
            <div className="empty-state">
              <p>No deployment usage has been reported yet.</p>
            </div>
          ) : (
            <ul className="metric-list">
              {sortedDeployments.map((deployment) => (
                <li key={deployment.id} className="metric-item">
                  <div className="item-headline">
                    <span className="item-title">{deployment.deploymentName}</span>
                    <span className="item-value">
                      {formatTokenCount(deployment.totalTokensLast24h)}
                    </span>
                  </div>
                  <div className="item-meta">
                    <span>{formatModelLabel(deployment.modelName, deployment.modelVersion)}</span>
                    <span>{deployment.hubName}</span>
                    <span>Latest metric {formatDateTime(deployment.lastMetricAt)}</span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Panel>

        <Panel title="Model mix" subtitle="Which model families are driving the deployment volume.">
          {modelTotals.length === 0 ? (
            <div className="empty-state">
              <p>No model activity is available yet.</p>
            </div>
          ) : (
            <ul className="project-list">
              {modelTotals.map(([modelName, total]) => (
                <li key={modelName} className="project-item">
                  <div className="item-headline">
                    <span className="item-title">{modelName}</span>
                    <span className="item-value">{formatTokenCount(total)}</span>
                  </div>
                  <p className="item-note">{formatExactTokens(total)} across active deployments</p>
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </section>
  )
}
