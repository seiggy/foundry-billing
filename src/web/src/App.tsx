import './App.css'
import { Agents } from './pages/Agents'
import { Dashboard } from './pages/Dashboard'
import { Projects } from './pages/Projects'
import { Reports } from './pages/Reports'
import { Sync } from './pages/Sync'
import { useHashRoute } from './hooks/useHashRoute'

const routes = ['dashboard', 'projects', 'reports', 'sync', 'agents'] as const

type RouteKey = (typeof routes)[number]

const navigation: ReadonlyArray<{
  key: RouteKey
  label: string
  description: string
}> = [
  { key: 'dashboard', label: 'Dashboard', description: 'Live token flow' },
  { key: 'projects', label: 'Projects', description: 'Hub and project inventory' },
  { key: 'reports', label: 'Reports', description: 'Deployment usage in the last 24h' },
  { key: 'sync', label: 'Sync', description: 'Worker status and run history' },
  { key: 'agents', label: 'Agents', description: 'Prompt and hosted agent inventory' },
]

function renderRoute(route: RouteKey) {
  switch (route) {
    case 'dashboard':
      return <Dashboard />
    case 'projects':
      return <Projects />
    case 'reports':
      return <Reports />
    case 'sync':
      return <Sync />
    case 'agents':
      return <Agents />
  }
}

function App() {
  const { route, navigate } = useHashRoute(routes, 'dashboard')

  return (
    <div className="app-shell">
      <header className="app-header">
        <div>
          <p className="app-kicker">Foundry Billing</p>
          <h1>Billing monitor</h1>
          <p className="app-summary">
            Live inventory and token usage from the API across hubs, projects, and deployments.
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
