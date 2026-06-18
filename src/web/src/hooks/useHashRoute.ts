import { useEffect, useMemo, useState } from 'react'

function normalizeHash(hash: string) {
  return hash.replace(/^#\/?/, '').trim().toLowerCase()
}

export function useHashRoute<T extends string>(
  availableRoutes: readonly T[],
  defaultRoute: T,
) {
  const routeSet = useMemo(
    () => new Set<string>(availableRoutes),
    [availableRoutes],
  )

  const resolveRoute = (hash: string): T => {
    const candidate = normalizeHash(hash)

    return routeSet.has(candidate) ? (candidate as T) : defaultRoute
  }

  const [route, setRoute] = useState<T>(() => resolveRoute(window.location.hash))

  useEffect(() => {
    const syncRoute = () => setRoute(resolveRoute(window.location.hash))

    syncRoute()
    window.addEventListener('hashchange', syncRoute)

    return () => {
      window.removeEventListener('hashchange', syncRoute)
    }
  }, [defaultRoute, routeSet])

  const navigate = (nextRoute: T) => {
    window.location.hash = `/${nextRoute}`
  }

  return { route, navigate }
}
