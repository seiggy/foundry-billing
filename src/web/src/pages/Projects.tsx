import { useCallback } from 'react'
import { billingClient } from '../api/client'
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
    return 'Never synced'
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

export function Projects() {
  const hubsRequest = useCallback(() => billingClient.getHubs(), [])
  const projectsRequest = useCallback(() => billingClient.getProjects(), [])

  const {
    data: hubs,
    loading: hubsLoading,
    error: hubsError,
    refresh: refreshHubs,
  } = useApi(hubsRequest)
  const {
    data: projects,
    loading: projectsLoading,
    error: projectsError,
    refresh: refreshProjects,
  } = useApi(projectsRequest)

  const refreshAll = () => {
    refreshHubs()
    refreshProjects()
  }

  const sortedHubs = [...(hubs ?? [])].sort(
    (left, right) => right.deploymentCount - left.deploymentCount || left.name.localeCompare(right.name),
  )
  const sortedProjects = [...(projects ?? [])].sort((left, right) => left.name.localeCompare(right.name))
  const latestSync = getLatestTimestamp([
    ...sortedHubs.map((hub) => hub.lastSyncedAt),
    ...sortedProjects.map((project) => project.lastSyncedAt),
  ])
  const errorMessage = hubsError ?? projectsError
  const isLoading = (hubsLoading && !hubs) || (projectsLoading && !projects)

  if (isLoading) {
    return (
      <section className="page">
        <header className="page-header">
          <div>
            <p className="page-eyebrow">Projects</p>
            <h2 className="page-title">Hub and project inventory</h2>
          </div>
          <p className="page-note">Loading hub topology…</p>
        </header>
      </section>
    )
  }

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="page-eyebrow">Projects</p>
          <h2 className="page-title">Hub and project inventory</h2>
        </div>
        <p className="page-note">
          {sortedHubs.length} hubs
          <br />
          Latest sync: {formatDateTime(latestSync)}
        </p>
      </header>

      {errorMessage ? (
        <Panel
          title="Inventory feed issue"
          subtitle={errorMessage}
          aside={
            <button type="button" className="action-button" onClick={refreshAll}>
              Retry
            </button>
          }
        >
          <div className="empty-state">
            <p>The inventory will repopulate as soon as the backend responds again.</p>
          </div>
        </Panel>
      ) : null}

      <section className="summary-grid" aria-label="Inventory summary">
        <article className="summary-card">
          <span className="summary-card-label">Hubs</span>
          <strong className="summary-card-value">{sortedHubs.length}</strong>
          <p className="summary-card-detail">Regional foundry containers under observation.</p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Projects</span>
          <strong className="summary-card-value">{sortedProjects.length}</strong>
          <p className="summary-card-detail">Projects mapped to a backing hub.</p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Deployments</span>
          <strong className="summary-card-value">
            {sortedHubs.reduce((total, hub) => total + hub.deploymentCount, 0)}
          </strong>
          <p className="summary-card-detail">Counted from the hub inventory feed.</p>
        </article>
      </section>

      <div className="project-grid">
        <Panel
          title="Hub list"
          subtitle="Deployment and project counts per foundry hub."
          aside={
            <button type="button" className="action-button" onClick={refreshAll}>
              Refresh
            </button>
          }
        >
          {sortedHubs.length === 0 ? (
            <div className="empty-state">
              <p>No hubs have been discovered yet.</p>
            </div>
          ) : (
            <ul className="project-list">
              {sortedHubs.map((hub) => (
                <li key={hub.id} className="project-item">
                  <div className="item-headline">
                    <span className="item-title">{hub.name}</span>
                    <span className="item-value">
                      {hub.deploymentCount} deployments / {hub.projectCount} projects
                    </span>
                  </div>
                  <div className="item-meta">
                    <span>{hub.region}</span>
                    <span className="mono">{hub.subscriptionId}</span>
                    <span>Synced {formatDateTime(hub.lastSyncedAt)}</span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Panel>

        <Panel title="Project map" subtitle="Projects grouped by their assigned hub.">
          {sortedProjects.length === 0 ? (
            <div className="empty-state">
              <p>No projects have been discovered yet.</p>
            </div>
          ) : (
            <ul className="project-list">
              {sortedProjects.map((project) => (
                <li key={project.id} className="project-item">
                  <div className="item-headline">
                    <span className="item-title">{project.name}</span>
                    <span className="item-value">{project.hubName}</span>
                  </div>
                  <div className="item-meta">
                    <span>{project.region}</span>
                    <span>Project ID: {project.id}</span>
                    <span>Synced {formatDateTime(project.lastSyncedAt)}</span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </section>
  )
}
