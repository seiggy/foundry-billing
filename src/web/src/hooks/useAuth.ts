import { useCallback, useEffect, useState } from 'react'

export interface User {
  name: string
  email: string
}

export function useAuth(): {
  user: User | null
  loading: boolean
  login: () => void
  logout: () => void
} {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function loadUser() {
      try {
        const response = await fetch('/auth/me', {
          headers: {
            Accept: 'application/json',
          },
          credentials: 'same-origin',
          signal: controller.signal,
        })

        if (response.status === 401) {
          setUser(null)
          return
        }

        if (!response.ok) {
          throw new Error(`Auth request failed with status ${response.status}`)
        }

        setUser((await response.json()) as User)
      } catch (error) {
        if (controller.signal.aborted) {
          return
        }

        console.error('Failed to load authenticated user.', error)
        setUser(null)
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    loadUser()

    return () => {
      controller.abort()
    }
  }, [])

  const login = useCallback(() => {
    window.location.href = '/auth/login'
  }, [])

  const logout = useCallback(() => {
    window.location.href = '/auth/logout'
  }, [])

  return { user, loading, login, logout }
}
