import { Panel } from '../components/Panel'

const reports = [
  {
    id: 'weekly-finance',
    name: 'Weekly finance rollup',
    cadence: 'Every Monday, 08:00',
    target: 'Finance Systems',
  },
  {
    id: 'project-drift',
    name: 'Project drift review',
    cadence: 'Every Wednesday, 13:00',
    target: 'Platform leads',
  },
]

export function Reports() {
  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="page-eyebrow">Reports</p>
          <h2 className="page-title">Scheduled exports and audit views</h2>
        </div>
        <p className="page-note">Placeholder route for recurring summaries and downloadable slices.</p>
      </header>

      <div className="page-grid">
        <Panel
          title="Upcoming report definitions"
          subtitle="This route gives the frontend a stable place for exports before charts arrive."
        >
          <ul className="report-list">
            {reports.map((report) => (
              <li key={report.id} className="report-item">
                <div className="item-headline">
                  <span className="item-title">{report.name}</span>
                  <span className="item-value">{report.target}</span>
                </div>
                <p>{report.cadence}</p>
              </li>
            ))}
          </ul>
        </Panel>

        <Panel
          title="Export backlog"
          subtitle="Starter content for CSV, ledger, and cost anomaly exports."
        >
          <div className="empty-state">
            <p>No export jobs wired yet.</p>
          </div>
        </Panel>
      </div>
    </section>
  )
}
