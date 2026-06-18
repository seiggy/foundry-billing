import { Panel } from '../components/Panel'
import type { FoundryProject } from '../types/billing'

const currencyFormatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
})

const projects: FoundryProject[] = [
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

function formatDate(date: string) {
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(date))
}

export function Projects() {
  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="page-eyebrow">Projects</p>
          <h2 className="page-title">Tracked foundry projects</h2>
        </div>
        <p className="page-note">Directory scaffold for list, filters, and project detail views.</p>
      </header>

      <div className="project-grid">
        <Panel
          title="Project list"
          subtitle="Typed shells ready for backend data once /api/billing/projects is live."
        >
          <ul className="project-list">
            {projects.map((project) => (
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

        <Panel
          title="Selected project scaffold"
          subtitle="Starter detail surface for budgets, rate changes, and metric drill-in."
        >
          <div className="detail-item">
            <div className="item-headline">
              <span className="item-title">FinOps Analyst</span>
              <span className="item-value">Needs review</span>
            </div>
            <div className="item-meta">
              <span>Owner: Finance Systems</span>
              <span>Last update: {formatDate('2026-06-18T09:15:00Z')}</span>
            </div>
          </div>

          <ul className="detail-list">
            <li className="detail-item">
              <div className="item-headline">
                <span className="item-title">Budget status</span>
                <span className="item-value">78%</span>
              </div>
              <p>Placeholder for budget threshold indicators and monthly pacing.</p>
            </li>
            <li className="detail-item">
              <div className="item-headline">
                <span className="item-title">Top metric</span>
                <span className="item-value">Reasoning burst</span>
              </div>
              <p>Placeholder for route-level charts and request token deltas.</p>
            </li>
          </ul>
        </Panel>
      </div>
    </section>
  )
}
