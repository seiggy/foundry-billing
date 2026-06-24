import './App.css'
import { Agents } from './pages/Agents'
import { Dashboard } from './pages/Dashboard'
import { Projects } from './pages/Projects'
import { PtuCalculator } from './pages/PtuCalculator'
import { Analytics } from './pages/Reports'
import { Sync } from './pages/Sync'
import { useAuth } from './hooks/useAuth'
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
  const { user, loading, login, logout } = useAuth()

  if (loading) {
    return (
      <div className="auth-shell">
        <main className="auth-card">
          <p className="app-kicker">Foundry Billing</p>
          <h1>Loading…</h1>
        </main>
      </div>
    )
  }

  if (!user) {
    return (
      <div className="auth-shell">
        <main className="auth-card">
          <p className="app-kicker">Foundry Billing</p>
          <h1>Foundry Billing</h1>
          <button type="button" className="action-button auth-button" onClick={login}>
            Sign in with Microsoft
          </button>
        </main>
      </div>
    )
  }

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header-copy">
          <p className="app-kicker">Foundry Billing</p>
          <h1>Billing monitor</h1>
        </div>
        <div className="user-badge" aria-label="Signed in user">
          <span className="user-name">{user.name}</span>
          <button type="button" className="user-link" onClick={logout}>
            Sign out
          </button>
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
