import { useCallback, useMemo, useState } from 'react'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { analyticsClient } from '../api/client'
import { Panel } from '../components/Panel'
import { useApi } from '../hooks/useApi'
import {
  formatCompactNumber,
  formatDate,
  formatDateTime,
  formatTokenCount,
  formatTokenExact,
  formatWholeNumber,
} from '../utils/format'

const DAY_OPTIONS = [30, 60, 90] as const

const SERIES_META = {
  promptTokens: { label: 'Prompt', color: '#4fd1c5' },
  completionTokens: { label: 'Completion', color: '#a78bfa' },
  totalTokens: { label: 'Total', color: '#f59e0b' },
} as const

type UsageSeriesKey = keyof typeof SERIES_META

function getSeriesLabel(name: string | number | undefined) {
  return name ? SERIES_META[name as UsageSeriesKey]?.label ?? String(name) : 'Value'
}

export function Analytics() {
  const [days, setDays] = useState<(typeof DAY_OPTIONS)[number]>(30)
  const usageRequest = useCallback(() => analyticsClient.getUsage(days), [days])
  const {
    data: usage,
    loading,
    error,
    refresh,
  } = useApi(usageRequest)

  const dailyUsage = useMemo(
    () => [...(usage?.dailyUsage ?? [])].sort((left, right) => left.date.localeCompare(right.date)),
    [usage],
  )
  const byModel = useMemo(
    () => [...(usage?.byModel ?? [])].sort((left, right) => right.totalTokens - left.totalTokens),
    [usage],
  )
  const byDeployment = useMemo(
    () =>
      [...(usage?.byDeployment ?? [])]
        .sort((left, right) => right.totalTokens - left.totalTokens)
        .slice(0, 15),
    [usage],
  )

  const topModel = byModel[0] ?? null
  const topDeployment = byDeployment[0] ?? null
  const averageDailyBurn =
    dailyUsage.length > 0 ? Math.round((usage?.totalTokens ?? 0) / dailyUsage.length) : 0
  const promptShare = usage && usage.totalTokens > 0 ? usage.totalPromptTokens / usage.totalTokens : 0
  const windowFacts = [
    { label: 'Average daily burn', value: formatTokenCount(averageDailyBurn) },
    { label: 'Prompt share', value: `${formatWholeNumber(promptShare * 100)}%` },
    { label: 'Tracked models', value: formatWholeNumber(byModel.length) },
    { label: 'Tracked deployments', value: formatWholeNumber(usage?.byDeployment.length ?? 0) },
  ]

  if (loading && !usage) {
    return (
      <section className="page">
        <header className="page-header">
          <div>
            <p className="page-eyebrow">Analytics</p>
            <h2 className="page-title">Usage observability</h2>
          </div>
          <p className="page-note">Loading rolling usage windows…</p>
        </header>
      </section>
    )
  }

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="page-eyebrow">Analytics</p>
          <h2 className="page-title">Usage observability</h2>
        </div>
        <p className="page-note">
          {formatDateTime(usage?.windowStart ?? null)} → {formatDateTime(usage?.windowEnd ?? null)}
          <br />
          Rolling {days}-day window
        </p>
      </header>

      <div className="analytics-toolbar">
        <div className="segmented-control" role="tablist" aria-label="Usage window selector">
          {DAY_OPTIONS.map((option) => (
            <button
              key={option}
              type="button"
              className={option === days ? 'segmented-button is-active' : 'segmented-button'}
              onClick={() => setDays(option)}
            >
              {option}d
            </button>
          ))}
        </div>
        <button type="button" className="action-button" onClick={refresh}>
          Refresh
        </button>
      </div>

      {error ? (
        <Panel
          title="Analytics feed issue"
          subtitle={error}
          aside={
            <button type="button" className="action-button" onClick={refresh}>
              Retry
            </button>
          }
        >
          <div className="empty-state">
            <p>Showing the last successful analytics payload where possible.</p>
          </div>
        </Panel>
      ) : null}

      <section className="summary-grid" aria-label="Usage analytics summary">
        <article className="summary-card">
          <span className="summary-card-label">Total tokens</span>
          <strong className="summary-card-value">{formatTokenCount(usage?.totalTokens ?? 0)}</strong>
          <p className="summary-card-detail mono">{formatTokenExact(usage?.totalTokens ?? 0)}</p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Prompt / completion</span>
          <strong className="summary-card-value mono">
            {formatCompactNumber(usage?.totalPromptTokens ?? 0)} /{' '}
            {formatCompactNumber(usage?.totalCompletionTokens ?? 0)}
          </strong>
          <p className="summary-card-detail">Prompt vs completion token volume</p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Heaviest model</span>
          <strong className="summary-card-value">{topModel?.modelName ?? 'No data'}</strong>
          <p className="summary-card-detail mono">
            {formatTokenExact(topModel?.totalTokens ?? 0)} across{' '}
            {formatWholeNumber(topModel?.deploymentCount ?? 0)} deployments
          </p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Top deployment</span>
          <strong className="summary-card-value">{topDeployment?.deploymentName ?? 'No data'}</strong>
          <p className="summary-card-detail mono">
            {formatTokenExact(topDeployment?.totalTokens ?? 0)} · {topDeployment?.hubName ?? '—'}
          </p>
        </article>
      </section>

      <Panel
        title="Usage over time"
        subtitle="Daily token burn by prompt, completion, and total volume."
        aside={<span className="status-text mono">{dailyUsage.length} daily slices</span>}
      >
        {dailyUsage.length === 0 ? (
          <div className="empty-state">
            <p>No daily usage has been reported for this window yet.</p>
          </div>
        ) : (
          <>
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={dailyUsage} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
                  <CartesianGrid stroke="#223047" strokeDasharray="3 3" />
                  <XAxis dataKey="date" tickFormatter={formatDate} minTickGap={28} stroke="#94a3b8" />
                  <YAxis tickFormatter={formatCompactNumber} stroke="#94a3b8" width={72} />
                  <Tooltip
                    labelFormatter={(label) => formatDate(String(label))}
                    formatter={(value, name) => [formatTokenExact(Number(value)), getSeriesLabel(name)]}
                    contentStyle={{
                      backgroundColor: '#0f1724',
                      border: '1px solid #223047',
                      borderRadius: '12px',
                    }}
                  />
                  <Legend />
                  <Line
                    type="monotone"
                    dataKey="promptTokens"
                    name={SERIES_META.promptTokens.label}
                    stroke={SERIES_META.promptTokens.color}
                    strokeWidth={2}
                    dot={false}
                    activeDot={{ r: 4 }}
                  />
                  <Line
                    type="monotone"
                    dataKey="completionTokens"
                    name={SERIES_META.completionTokens.label}
                    stroke={SERIES_META.completionTokens.color}
                    strokeWidth={2}
                    dot={false}
                    activeDot={{ r: 4 }}
                  />
                  <Line
                    type="monotone"
                    dataKey="totalTokens"
                    name={SERIES_META.totalTokens.label}
                    stroke={SERIES_META.totalTokens.color}
                    strokeWidth={2}
                    dot={false}
                    activeDot={{ r: 4 }}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
            <p className="chart-note">
              Lines stay raw. No smoothing, no percent-of-total tricks — this is the actual burn
              curve.
            </p>
          </>
        )}
      </Panel>

      <div className="page-grid">
        <Panel title="Model breakdown" subtitle="Stacked prompt and completion usage by model family.">
          {byModel.length === 0 ? (
            <div className="empty-state">
              <p>No model usage is available for this window yet.</p>
            </div>
          ) : (
            <div className="chart-wrap is-compact">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart
                  data={byModel.slice(0, 8)}
                  layout="vertical"
                  margin={{ top: 8, right: 16, bottom: 8, left: 32 }}
                >
                  <CartesianGrid stroke="#223047" strokeDasharray="3 3" />
                  <XAxis type="number" tickFormatter={formatCompactNumber} stroke="#94a3b8" />
                  <YAxis
                    type="category"
                    dataKey="modelName"
                    width={132}
                    tick={{ fill: '#e2e8f0', fontSize: 12 }}
                    stroke="#94a3b8"
                  />
                  <Tooltip
                    formatter={(value, name) => [formatTokenExact(Number(value)), getSeriesLabel(name)]}
                    contentStyle={{
                      backgroundColor: '#0f1724',
                      border: '1px solid #223047',
                      borderRadius: '12px',
                    }}
                  />
                  <Legend />
                  <Bar
                    dataKey="promptTokens"
                    stackId="usage"
                    name={SERIES_META.promptTokens.label}
                    fill={SERIES_META.promptTokens.color}
                  />
                  <Bar
                    dataKey="completionTokens"
                    stackId="usage"
                    name={SERIES_META.completionTokens.label}
                    fill={SERIES_META.completionTokens.color}
                    radius={[0, 6, 6, 0]}
                  />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </Panel>

        <Panel title="Window facts" subtitle="Fast readouts for the current analytics slice.">
          <ul className="detail-list fact-list">
            {windowFacts.map((fact) => (
              <li key={fact.label} className="detail-item fact-item">
                <span className="fact-label">{fact.label}</span>
                <strong className="fact-value mono">{fact.value}</strong>
              </li>
            ))}
          </ul>
        </Panel>
      </div>

      <Panel
        title="Deployment table"
        subtitle="Top deployments by total token usage in the selected window."
        aside={<span className="status-text mono">{formatWholeNumber(byDeployment.length)} rows shown</span>}
      >
        {byDeployment.length === 0 ? (
          <div className="empty-state">
            <p>No deployment usage has been reported for this window yet.</p>
          </div>
        ) : (
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Deployment</th>
                  <th>Model</th>
                  <th>Hub</th>
                  <th className="mono">Prompt</th>
                  <th className="mono">Completion</th>
                  <th className="mono">Total</th>
                </tr>
              </thead>
              <tbody>
                {byDeployment.map((deployment) => (
                  <tr key={`${deployment.deploymentName}-${deployment.modelName}-${deployment.hubName}`}>
                    <td>
                      <div className="table-cell-stack">
                        <span className="item-title">{deployment.deploymentName}</span>
                        <span className="table-secondary">{formatTokenCount(deployment.totalTokens)}</span>
                      </div>
                    </td>
                    <td>{deployment.modelName}</td>
                    <td>{deployment.hubName}</td>
                    <td className="mono">{formatTokenExact(deployment.promptTokens)}</td>
                    <td className="mono">{formatTokenExact(deployment.completionTokens)}</td>
                    <td className="mono">{formatTokenExact(deployment.totalTokens)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>
    </section>
  )
}
