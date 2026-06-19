import './App.css'
import { Agents } from './pages/Agents'
import { Dashboard } from './pages/Dashboard'
import { Projects } from './pages/Projects'
import { PtuCalculator } from './pages/PtuCalculator'
import { Analytics } from './pages/Reports'
import { Sync } from './pages/Sync'
import { useHashRoute } from './hooks/useHashRoute'

const routes = ['dashboard', 'projects', 'agents', 'analytics', 'ptu-calc', 'sync'] as const

type RouteKey = (typeof routes)[number]

const navigation: ReadonlyArray<{
  key: RouteKey
  label: string
  description: string
}> = [
  { key: 'dashboard', label: 'Dashboard', description: 'Live token flow' },
  { key: 'projects', label: 'Projects', description: 'Hub and project inventory' },
  { key: 'agents', label: 'Agents', description: 'Prompt and hosted agent inventory' },
  { key: 'analytics', label: 'Analytics', description: 'Usage curves, model mix, deployment burn' },
  { key: 'ptu-calc', label: 'PTU Calc', description: 'TPM sizing, pricing, and reserved capacity' },
  { key: 'sync', label: 'Sync', description: 'Worker status and run history' },
]

function renderRoute(route: RouteKey) {
  switch (route) {
    case 'dashboard':
      return <Dashboard />
    case 'projects':
      return <Projects />
    case 'agents':
      return <Agents />
    case 'analytics':
      return <Analytics />
    case 'ptu-calc':
      return <PtuCalculator />
    case 'sync':
      return <Sync />
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
            Live inventory and token usage from the API across hubs, projects, deployments, and
            PTU planning surfaces.
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
