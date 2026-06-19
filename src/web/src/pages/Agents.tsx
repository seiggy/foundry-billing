import { useCallback, useMemo, useState } from 'react'
import { billingClient } from '../api/client'
import { Panel } from '../components/Panel'
import { useApi } from '../hooks/useApi'
import type { FoundryAgent } from '../types/billing'

const dateFormatter = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  year: 'numeric',
})

const dateTimeFormatter = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  year: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
})

type KindFilter = 'All' | 'Prompt' | 'Hosted' | 'Unknown'

function formatDate(value: string | null) {
  if (!value) {
    return '—'
  }

  return dateFormatter.format(new Date(value))
}

function formatDateTime(value: string | null) {
  if (!value) {
    return '—'
  }

  return dateTimeFormatter.format(new Date(value))
}

function getLatestTimestamp(values: Array<string | null>) {
  const timestamps = values.filter((value): value is string => value !== null)

  if (timestamps.length === 0) {
    return null
  }

  return [...timestamps].sort((left, right) => Date.parse(right) - Date.parse(left))[0]
}

function getKindLabel(kind: FoundryAgent['kind']) {
  return kind ?? 'Unknown'
}

function getKindTone(kind: FoundryAgent['kind']) {
  switch (kind) {
    case 'Hosted':
      return 'is-hosted'
    case 'Prompt':
      return 'is-prompt'
    default:
      return 'is-unknown'
  }
}

export function Agents() {
  const agentsRequest = useCallback(() => billingClient.getAgents(), [])
  const { data: agents, loading, error, refresh } = useApi(agentsRequest)
  const [projectFilter, setProjectFilter] = useState('All')
  const [hubFilter, setHubFilter] = useState('All')
  const [kindFilter, setKindFilter] = useState<KindFilter>('All')

  const sortedAgents = useMemo(
    () =>
      [...(agents ?? [])].sort(
        (left, right) =>
          left.projectName.localeCompare(right.projectName) ||
          left.hubName.localeCompare(right.hubName) ||
          left.name.localeCompare(right.name),
      ),
    [agents],
  )

  const projectOptions = useMemo(
    () => Array.from(new Set(sortedAgents.map((agent) => agent.projectName))).sort(),
    [sortedAgents],
  )
  const hubOptions = useMemo(
    () => Array.from(new Set(sortedAgents.map((agent) => agent.hubName))).sort(),
    [sortedAgents],
  )

  const filteredAgents = useMemo(
    () =>
      sortedAgents.filter((agent) => {
        const matchesProject = projectFilter === 'All' || agent.projectName === projectFilter
        const matchesHub = hubFilter === 'All' || agent.hubName === hubFilter
        const matchesKind =
          kindFilter === 'All' ||
          (kindFilter === 'Unknown' ? agent.kind === null : agent.kind === kindFilter)

        return matchesProject && matchesHub && matchesKind
      }),
    [hubFilter, kindFilter, projectFilter, sortedAgents],
  )

  const kindCounts = useMemo(
    () =>
      filteredAgents.reduce(
        (counts, agent) => {
          switch (agent.kind) {
            case 'Hosted':
              counts.hosted += 1
              break
            case 'Prompt':
              counts.prompt += 1
              break
            default:
              counts.unknown += 1
              break
          }

          return counts
        },
        { prompt: 0, hosted: 0, unknown: 0 },
      ),
    [filteredAgents],
  )

  const modelCounts = useMemo(
    () =>
      Array.from(
        filteredAgents.reduce((groups, agent) => {
          const label = agent.modelName ?? 'Unassigned'
          const currentTotal = groups.get(label) ?? 0
          groups.set(label, currentTotal + 1)
          return groups
        }, new Map<string, number>()),
      ).sort((left, right) => right[1] - left[1] || left[0].localeCompare(right[0])),
    [filteredAgents],
  )

  const projectsInView = new Set(filteredAgents.map((agent) => agent.projectName)).size
  const hubsInView = new Set(filteredAgents.map((agent) => agent.hubName)).size
  const latestSync = getLatestTimestamp(filteredAgents.map((agent) => agent.lastSyncedAt))
  const latestCreated = getLatestTimestamp(filteredAgents.map((agent) => agent.createdAt))
  const topModels = modelCounts.slice(0, 3)
  const filtersActive =
    projectFilter !== 'All' || hubFilter !== 'All' || kindFilter !== 'All'

  if (loading && !agents) {
    return (
      <section className="page">
        <header className="page-header">
          <div>
            <p className="page-eyebrow">Agents</p>
            <h2 className="page-title">Agent inventory</h2>
          </div>
          <p className="page-note">Loading prompt and hosted agents…</p>
        </header>
      </section>
    )
  }

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="page-eyebrow">Agents</p>
          <h2 className="page-title">Agent inventory</h2>
        </div>
        <p className="page-note">
          {filteredAgents.length} rows in view
          <br />
          Latest sync: {formatDateTime(latestSync)}
        </p>
      </header>

      {error ? (
        <Panel
          title="Agent feed issue"
          subtitle={error}
          aside={
            <button type="button" className="action-button" onClick={refresh}>
              Retry
            </button>
          }
        >
          <div className="empty-state">
            <p>Existing agent inventory stays visible until the feed responds again.</p>
          </div>
        </Panel>
      ) : null}

      <section className="summary-grid" aria-label="Agent summary">
        <article className="summary-card">
          <span className="summary-card-label">Agents</span>
          <strong className="summary-card-value">{filteredAgents.length}</strong>
          <p className="summary-card-detail">
            {projectsInView} projects across {hubsInView} hubs in the current view.
          </p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Kind breakdown</span>
          <strong className="summary-card-value">
            {kindCounts.prompt} / {kindCounts.hosted}
          </strong>
          <p className="summary-card-detail">
            Prompt {kindCounts.prompt} · Hosted {kindCounts.hosted} · Unknown {kindCounts.unknown}
          </p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Models used</span>
          <strong className="summary-card-value">{modelCounts.length}</strong>
          <p className="summary-card-detail">
            {topModels.length > 0
              ? topModels.map(([modelName, count]) => `${modelName} (${count})`).join(' · ')
              : 'No model names reported yet.'}
          </p>
        </article>
      </section>

      <Panel
        title="Filters"
        subtitle="Client-side filters for the discovered agent inventory."
        aside={
          filtersActive ? (
            <button
              type="button"
              className="action-button"
              onClick={() => {
                setProjectFilter('All')
                setHubFilter('All')
                setKindFilter('All')
              }}
            >
              Clear filters
            </button>
          ) : null
        }
      >
        <div className="filters-bar">
          <label className="filter-field">
            <span>Project</span>
            <select
              className="filter-select"
              value={projectFilter}
              onChange={(event) => setProjectFilter(event.target.value)}
            >
              <option value="All">All projects</option>
              {projectOptions.map((projectName) => (
                <option key={projectName} value={projectName}>
                  {projectName}
                </option>
              ))}
            </select>
          </label>

          <label className="filter-field">
            <span>Hub</span>
            <select
              className="filter-select"
              value={hubFilter}
              onChange={(event) => setHubFilter(event.target.value)}
            >
              <option value="All">All hubs</option>
              {hubOptions.map((hubName) => (
                <option key={hubName} value={hubName}>
                  {hubName}
                </option>
              ))}
            </select>
          </label>

          <label className="filter-field">
            <span>Kind</span>
            <select
              className="filter-select"
              value={kindFilter}
              onChange={(event) => setKindFilter(event.target.value as KindFilter)}
            >
              <option value="All">All kinds</option>
              <option value="Prompt">Prompt</option>
              <option value="Hosted">Hosted</option>
              <option value="Unknown">Unknown</option>
            </select>
          </label>
        </div>
      </Panel>

      <Panel
        title="Agent table"
        subtitle={`Sorted by project, hub, then agent name. Latest created record: ${formatDate(latestCreated)}.`}
        aside={
          <button type="button" className="action-button" onClick={refresh}>
            Refresh
          </button>
        }
      >
        {filteredAgents.length === 0 ? (
          <div className="empty-state">
            <p>
              {filtersActive
                ? 'No agents match the current filters.'
                : 'No agents have been discovered yet.'}
            </p>
          </div>
        ) : (
          <div className="table-scroll">
            <table className="agents-table">
              <thead>
                <tr>
                  <th>Agent</th>
                  <th>Kind</th>
                  <th>Model</th>
                  <th>Project</th>
                  <th>Hub</th>
                  <th className="mono">Created</th>
                  <th className="mono">Last synced</th>
                </tr>
              </thead>
              <tbody>
                {filteredAgents.map((agent) => (
                  <tr key={agent.id}>
                    <td>
                      <div className="table-cell-stack">
                        <span className="item-title">{agent.name}</span>
                        <span className="table-secondary mono">{agent.agentId}</span>
                        {agent.description ? (
                          <span className="table-secondary">{agent.description}</span>
                        ) : null}
                      </div>
                    </td>
                    <td>
                      <span className={`kind-pill ${getKindTone(agent.kind)}`}>
                        {getKindLabel(agent.kind)}
                      </span>
                    </td>
                    <td>{agent.modelName ?? 'Unassigned'}</td>
                    <td>{agent.projectName}</td>
                    <td>{agent.hubName}</td>
                    <td className="mono">{formatDate(agent.createdAt)}</td>
                    <td className="mono">{formatDateTime(agent.lastSyncedAt)}</td>
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
