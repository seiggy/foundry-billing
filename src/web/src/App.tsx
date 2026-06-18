import './App.css'
import { Dashboard } from './pages/Dashboard'
import { Projects } from './pages/Projects'
import { Reports } from './pages/Reports'
import { useHashRoute } from './hooks/useHashRoute'

const routes = ['dashboard', 'projects', 'reports'] as const

type RouteKey = (typeof routes)[number]

const navigation: ReadonlyArray<{
  key: RouteKey
  label: string
  description: string
}> = [
  { key: 'dashboard', label: 'Dashboard', description: 'Tenant-wide cost signals' },
  { key: 'projects', label: 'Projects', description: 'Project coverage and owners' },
  { key: 'reports', label: 'Reports', description: 'Scheduled exports and audits' },
]

function renderRoute(route: RouteKey) {
  switch (route) {
    case 'dashboard':
      return <Dashboard />
    case 'projects':
      return <Projects />
    case 'reports':
      return <Reports />
  }
}

function App() {
  const { route, navigate } = useHashRoute(routes, 'dashboard')

  return (
    <div className="app-shell">
      <header className="app-header">
        <div>
          <p className="app-kicker">Foundry Billing</p>
          <h1>Tenant billing observability</h1>
          <p className="app-summary">
            Frontend scaffold for billing metrics, project drill-in, and reporting views.
          </p>
        </div>
        <div className="app-meta">
          <span className="meta-pill">Vite + React + TypeScript</span>
          <span className="meta-pill">API via /api proxy</span>
        </div>
      </header>

      <nav className="view-tabs" aria-label="Primary">
        {navigation.map((item) => (
          <button
            key={item.key}
            type="button"
            className={item.key === route ? 'view-tab is-active' : 'view-tab'}
            onClick={() => navigate(item.key)}
          >
            <span className="view-tab-label">{item.label}</span>
            <span className="view-tab-description">{item.description}</span>
          </button>
        ))}
      </nav>

      <main className="app-main">{renderRoute(route)}</main>
    </div>
  )
}

export default App
