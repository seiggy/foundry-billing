import { Panel } from '../components/Panel'
import type { BillingMetric, FoundryProject, UsageSummary } from '../types/billing'

const currencyFormatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
})

const numberFormatter = new Intl.NumberFormat('en-US')

const recentMetrics: BillingMetric[] = [
  {
    id: 'metric-gpt4o-input',
    projectId: 'proj-chatops',
    projectName: 'ChatOps',
    category: 'tokens',
    name: 'GPT-4o input tokens',
    value: 1824000,
    unit: 'tokens',
    cost: 4120,
    currency: 'USD',
    recordedAt: '2026-06-18T14:00:00Z',
  },
  {
    id: 'metric-embeddings',
    projectId: 'proj-search',
    projectName: 'Vector Search',
    category: 'requests',
    name: 'Embedding refresh',
    value: 18200,
    unit: 'requests',
    cost: 1680,
    currency: 'USD',
    recordedAt: '2026-06-18T12:30:00Z',
  },
  {
    id: 'metric-reasoning',
    projectId: 'proj-finops',
    projectName: 'FinOps Analyst',
    category: 'tokens',
    name: 'Reasoning burst',
    value: 968000,
    unit: 'tokens',
    cost: 990,
    currency: 'USD',
    recordedAt: '2026-06-18T09:15:00Z',
  },
]

const trackedProjects: FoundryProject[] = [
  {
    id: 'proj-chatops',
    name: 'ChatOps',
    owner: 'Support Engineering',
    region: 'eastus2',
    environment: 'production',
    status: 'healthy',
    totalCost: 7520,
    currency: 'USD',
    lastUpdated: '2026-06-18T14:00:00Z',
  },
  {
    id: 'proj-search',
    name: 'Vector Search',
    owner: 'Search Platform',
    region: 'westus3',
    environment: 'production',
    status: 'healthy',
    totalCost: 6240,
    currency: 'USD',
    lastUpdated: '2026-06-18T12:30:00Z',
  },
  {
    id: 'proj-finops',
    name: 'FinOps Analyst',
    owner: 'Finance Systems',
    region: 'centralus',
    environment: 'staging',
    status: 'watch',
    totalCost: 4660,
    currency: 'USD',
    lastUpdated: '2026-06-18T09:15:00Z',
  },
]

const summary: UsageSummary = {
  tenantId: 'tenant-primary',
  windowStart: '2026-06-01T00:00:00Z',
  windowEnd: '2026-06-30T23:59:59Z',
  totalCost: 18420,
  currency: 'USD',
  projectCount: trackedProjects.length,
  metrics: recentMetrics,
  projects: trackedProjects,
}

function formatDate(date: string) {
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(date))
}

export function Dashboard() {
  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="page-eyebrow">Dashboard</p>
          <h2 className="page-title">Current billing window</h2>
        </div>
        <p className="page-note">
          {summary.tenantId}
          <br />
          Jun 1 – Jun 30
        </p>
      </header>

      <section className="summary-grid" aria-label="Usage summary">
        <article className="summary-card">
          <span className="summary-card-label">Total spend</span>
          <strong className="summary-card-value">
            {currencyFormatter.format(summary.totalCost)}
          </strong>
          <p className="summary-card-detail">Across {summary.projectCount} tracked projects.</p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Recent metrics</span>
          <strong className="summary-card-value">
            {numberFormatter.format(summary.metrics.length)}
          </strong>
          <p className="summary-card-detail">Latest observations ready for charts and drill-in.</p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Projects in focus</span>
          <strong className="summary-card-value">
            {trackedProjects.filter((project) => project.status !== 'healthy').length}
          </strong>
          <p className="summary-card-detail">Projects that may need budget or usage review.</p>
        </article>
      </section>

      <div className="page-grid">
        <Panel
          title="Recent cost signals"
          subtitle="Starter structure for the metrics feed coming from /api/billing/metrics."
        >
          <ul className="metric-list">
            {recentMetrics.map((metric) => (
              <li key={metric.id} className="metric-item">
                <div className="item-headline">
                  <span className="item-title">{metric.name}</span>
                  <span className="item-value">{currencyFormatter.format(metric.cost)}</span>
                </div>
                <div className="item-meta">
                  <span>{metric.projectName}</span>
                  <span>
                    {numberFormatter.format(metric.value)} {metric.unit}
                  </span>
                  <span>{formatDate(metric.recordedAt)}</span>
                </div>
              </li>
            ))}
          </ul>
        </Panel>

        <Panel
          title="Project coverage"
          subtitle="Placeholder layout for the first tenant-wide project overview."
        >
          <ul className="project-list">
            {trackedProjects.map((project) => (
              <li key={project.id} className="project-item">
                <div className="item-headline">
                  <span className="item-title">{project.name}</span>
                  <span className="item-value">{currencyFormatter.format(project.totalCost)}</span>
                </div>
                <div className="item-meta">
                  <span>{project.owner}</span>
                  <span>{project.region}</span>
                  <span>{project.environment}</span>
                </div>
              </li>
            ))}
          </ul>
        </Panel>
      </div>
    </section>
  )
}
