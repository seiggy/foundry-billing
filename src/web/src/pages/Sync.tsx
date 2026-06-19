import { useCallback, useEffect, useMemo, useState } from 'react'
import { ApiError, syncClient } from '../api/client'
import { Panel } from '../components/Panel'
import { useApi } from '../hooks/useApi'

const dateTimeFormatter = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
})

function formatDateTime(value: string | null) {
  if (!value) {
    return '—'
  }

  return dateTimeFormatter.format(new Date(value))
}

function formatDuration(startedAt: string, completedAt: string | null) {
  const startedAtMs = Date.parse(startedAt)
  const completedAtMs = Date.parse(completedAt ?? new Date().toISOString())

  if (Number.isNaN(startedAtMs) || Number.isNaN(completedAtMs)) {
    return '—'
  }

  const durationMs = Math.max(0, completedAtMs - startedAtMs)
  const totalSeconds = Math.floor(durationMs / 1000)
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60

  if (hours > 0) {
    return `${hours}h ${minutes}m`
  }

  if (minutes > 0) {
    return `${minutes}m ${seconds}s`
  }

  return `${seconds}s`
}

function getStatusTone(status: string) {
  switch (status.toLowerCase()) {
    case 'running':
      return 'is-running'
    case 'failed':
      return 'is-failed'
    default:
      return 'is-completed'
  }
}

function toErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    return error.details || error.message
  }

  if (error instanceof Error) {
    return error.message
  }

  if (typeof error === 'string') {
    return error
  }

  return 'Unexpected error while running the sync action.'
}

export function Sync() {
  const statusRequest = useCallback(() => syncClient.getStatus(), [])
  const historyRequest = useCallback(() => syncClient.getHistory(), [])
  const {
    data: status,
    loading: statusLoading,
    error: statusError,
    refresh: refreshStatus,
  } = useApi(statusRequest)
  const {
    data: history,
    loading: historyLoading,
    error: historyError,
    refresh: refreshHistory,
  } = useApi(historyRequest)
  const [isTriggering, setIsTriggering] = useState(false)
  const [triggerNotice, setTriggerNotice] = useState<string | null>(null)
  const [triggerError, setTriggerError] = useState<string | null>(null)

  const refreshAll = useCallback(() => {
    refreshStatus()
    refreshHistory()
  }, [refreshHistory, refreshStatus])

  useEffect(() => {
    if (!status?.isRunning) {
      return
    }

    const intervalId = window.setInterval(() => {
      refreshAll()
    }, 5000)

    return () => {
      window.clearInterval(intervalId)
    }
  }, [refreshAll, status?.isRunning])

  useEffect(() => {
    if (!triggerNotice) {
      return
    }

    const timeoutId = window.setTimeout(() => {
      setTriggerNotice(null)
    }, 8000)

    return () => {
      window.clearTimeout(timeoutId)
    }
  }, [triggerNotice])

  const handleTrigger = useCallback(async () => {
    setIsTriggering(true)
    setTriggerError(null)

    try {
      const response = await syncClient.trigger()
      setTriggerNotice(`Manual sync accepted. Run ${response.runId} is queued.`)
      refreshAll()
    } catch (error) {
      setTriggerError(toErrorMessage(error))
    } finally {
      setIsTriggering(false)
    }
  }, [refreshAll])

  const runs = useMemo(
    () =>
      [...(history?.runs ?? [])].sort(
        (left, right) => Date.parse(right.startedAt) - Date.parse(left.startedAt),
      ),
    [history],
  )
  const activeRun = status?.currentRun ?? runs.find((run) => run.status === 'Running') ?? null
  const activeRunStartedAt = activeRun?.startedAt ?? null
  const isLoading = (statusLoading && !status) || (historyLoading && !history)
  const pageError = triggerError ?? statusError ?? historyError

  if (isLoading) {
    return (
      <section className="page">
        <header className="page-header">
          <div>
            <p className="page-eyebrow">Sync</p>
            <h2 className="page-title">Sync management</h2>
          </div>
          <p className="page-note">Loading sync worker state…</p>
        </header>
      </section>
    )
  }

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="page-eyebrow">Sync</p>
          <h2 className="page-title">Sync management</h2>
        </div>
        <p className="page-note">
          Manual control and run log
          <br />
          Auto-refresh every 5 seconds while a run is active
        </p>
      </header>

      {pageError ? (
        <Panel
          title="Sync control issue"
          subtitle={pageError}
          aside={
            <button type="button" className="action-button" onClick={refreshAll}>
              Retry
            </button>
          }
        >
          <div className="empty-state">
            <p>Last successful sync data stays visible when the worker endpoints are reachable again.</p>
          </div>
        </Panel>
      ) : null}

      {triggerNotice ? <div className="sync-banner is-success">{triggerNotice}</div> : null}

      <Panel
        title="Sync status"
        subtitle="Worker state, current run, and the last completed sync."
        aside={
          <div className="sync-actions">
            <button
              type="button"
              className="action-button"
              onClick={handleTrigger}
              disabled={isTriggering || status?.isRunning === true}
            >
              {status?.isRunning ? 'Sync running' : isTriggering ? 'Triggering…' : 'Run Sync Now'}
            </button>
            <button type="button" className="action-button" onClick={refreshAll}>
              Refresh
            </button>
          </div>
        }
      >
        <div className="sync-panel">
          <div className="sync-status-grid">
            <article className="sync-status-card">
              <span className="sync-status-card-label">Worker</span>
              <strong className="sync-status-card-value">
                {status?.isRunning ? 'Running' : 'Idle'}
              </strong>
              <div className="sync-status-card-meta">
                <span
                  className={`status-pill ${getStatusTone(
                    status?.currentRun?.status ?? (status?.isRunning ? 'Running' : 'Completed'),
                  )}`}
                >
                  {activeRun?.status ?? (status?.isRunning ? 'Running' : 'Idle')}
                </span>
                <span>
                  {status?.isRunning
                    ? 'Polling every 5 seconds'
                    : 'Waiting for a manual or scheduled run'}
                </span>
              </div>
            </article>

            <article className="sync-status-card">
              <span className="sync-status-card-label">Current run</span>
              <strong className="sync-status-card-value">{activeRun?.id ?? 'None'}</strong>
              <div className="sync-status-card-meta">
                <span>Started {formatDateTime(activeRun?.startedAt ?? null)}</span>
                <span>
                  {activeRunStartedAt
                    ? `Elapsed ${formatDuration(activeRunStartedAt, null)}`
                    : 'No active run'}
                </span>
              </div>
            </article>

            <article className="sync-status-card">
              <span className="sync-status-card-label">Last completed</span>
              <strong className="sync-status-card-value">
                {status?.lastCompletedAt ? formatDateTime(status.lastCompletedAt) : 'Never'}
              </strong>
              <div className="sync-status-card-meta">
                <span>Current status {activeRun?.status ?? 'Idle'}</span>
                <span>{runs.length} recorded runs</span>
              </div>
            </article>
          </div>
        </div>
      </Panel>

      <Panel
        title="Run history"
        subtitle="Newest first. Stats are pulled directly from the sync history endpoint."
      >
        {runs.length === 0 ? (
          <div className="empty-state">
            <p>No sync runs have been recorded yet.</p>
          </div>
        ) : (
          <div className="table-scroll">
            <table className="sync-table">
              <thead>
                <tr>
                  <th>Run</th>
                  <th className="mono">Started</th>
                  <th className="mono">Completed</th>
                  <th className="mono">Duration</th>
                  <th>Status</th>
                  <th>Stats</th>
                  <th>Error</th>
                </tr>
              </thead>
              <tbody>
                {runs.map((run) => (
                  <tr key={run.id}>
                    <td className="mono">
                      <span className="sync-run-id">{run.id}</span>
                    </td>
                    <td className="mono">{formatDateTime(run.startedAt)}</td>
                    <td className="mono">{formatDateTime(run.completedAt)}</td>
                    <td className="mono">{formatDuration(run.startedAt, run.completedAt)}</td>
                    <td>
                      <span className={`status-pill ${getStatusTone(run.status)}`}>{run.status}</span>
                    </td>
                    <td>
                      <div className="sync-table-stats">
                        <span>{run.hubsDiscovered} hubs</span>
                        <span>{run.projectsDiscovered} projects</span>
                        <span>{run.deploymentsDiscovered} deployments</span>
                        <span>{run.usageSlicesInserted} slices</span>
                      </div>
                    </td>
                    <td>
                      <div className="sync-table-stack">
                        <span>{run.errorMessage ?? '—'}</span>
                        {run.completedAt ? (
                          <span className="sync-table-secondary">Finished {formatDateTime(run.completedAt)}</span>
                        ) : null}
                      </div>
                    </td>
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
