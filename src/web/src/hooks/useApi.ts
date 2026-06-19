import { useCallback, useEffect, useState } from 'react'

function toErrorMessage(error: unknown) {
  if (typeof error === 'string') {
    return error
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Unexpected error while loading data.'
}

export function useApi<T>(
  fetcher: () => Promise<T>,
): {
  data: T | null
  loading: boolean
  error: string | null
  refresh: () => void
} {
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [refreshToken, setRefreshToken] = useState(0)

  const refresh = useCallback(() => {
    setLoading(true)
    setError(null)
    setRefreshToken((value) => value + 1)
  }, [])

  useEffect(() => {
    let isActive = true

    fetcher()
      .then((response) => {
        if (!isActive) {
          return
        }

        setData(response)
      })
      .catch((fetchError: unknown) => {
        if (!isActive) {
          return
        }

        setError(toErrorMessage(fetchError))
      })
      .finally(() => {
        if (!isActive) {
          return
        }

        setLoading(false)
      })

    return () => {
      isActive = false
    }
  }, [fetcher, refreshToken])

  return { data, loading, error, refresh }
}
