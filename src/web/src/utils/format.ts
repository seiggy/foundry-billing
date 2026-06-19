const compactNumberFormatter = new Intl.NumberFormat('en-US', {
  notation: 'compact',
  maximumFractionDigits: 1,
})

const wholeNumberFormatter = new Intl.NumberFormat('en-US', {
  maximumFractionDigits: 0,
})

const currencyFormatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
})

const shortDateFormatter = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
})

const dateTimeFormatter = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
})

function toDate(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? new Date(`${value}T00:00:00`) : new Date(value)
}

export function formatCompactNumber(value: number) {
  return compactNumberFormatter.format(value)
}

export function formatWholeNumber(value: number) {
  return wholeNumberFormatter.format(Math.round(value))
}

export function formatTokenCount(value: number) {
  return `${formatCompactNumber(value)} tokens`
}

export function formatTokenExact(value: number) {
  return `${formatWholeNumber(value)} tokens`
}

export function formatCurrencyAmount(value: number) {
  return currencyFormatter.format(value)
}

export function formatCurrency(value: number) {
  return `${formatCurrencyAmount(value)}/mo`
}

export function formatDate(value: string) {
  return shortDateFormatter.format(toDate(value))
}

export function formatDateTime(value: string | null) {
  if (!value) {
    return 'No data yet'
  }

  return dateTimeFormatter.format(toDate(value))
}
