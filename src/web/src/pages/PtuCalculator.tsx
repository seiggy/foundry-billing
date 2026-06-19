import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { analyticsClient } from '../api/client'
import { Panel } from '../components/Panel'
import { useApi } from '../hooks/useApi'
import type {
  DeploymentType,
  PtuCalculationRequest,
  PtuRecommendation,
  PtuRecommendationCostComparison,
  TpmAnalyticsModel,
} from '../types/billing'
import {
  formatCompactNumber,
  formatCurrency,
  formatCurrencyAmount,
  formatTokenCount,
  formatWholeNumber,
} from '../utils/format'

const DAY_OPTIONS = [30, 60, 90] as const
const DEPLOYMENT_TYPES: DeploymentType[] = ['Global', 'Data Zone', 'Regional']

interface OverrideDraft {
  inputRate: string
  outputRate: string
  tpmPerPtu: string
  deploymentType: DeploymentType
}

function toErrorMessage(error: unknown) {
  if (typeof error === 'string') {
    return error
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Unexpected error while calculating PTU recommendations.'
}

function parseNullableNumber(value: string) {
  if (!value.trim()) {
    return null
  }

  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : null
}

function isInvalidNumericInput(value: string) {
  return value.trim().length > 0 && Number.isNaN(Number(value))
}

function formatUtilization(value: number) {
  const ratio = value > 1 ? value / 100 : value
  return `${formatWholeNumber(ratio * 100)}%`
}

function buildRequest(
  days: number,
  models: TpmAnalyticsModel[],
  overrides: Record<string, OverrideDraft>,
): PtuCalculationRequest {
  return {
    days,
    models: models.map((model) => {
      const override = overrides[model.modelName]

      return {
        modelName: model.modelName,
        totalTokens: model.totalTokens,
        avgTpm: model.avgTpm,
        p99Tpm: model.p99Tpm,
        inputRate: parseNullableNumber(override?.inputRate ?? ''),
        outputRate: parseNullableNumber(override?.outputRate ?? ''),
        tpmPerPtu: parseNullableNumber(override?.tpmPerPtu ?? ''),
        deploymentType: override?.deploymentType ?? 'Global',
      }
    }),
  }
}

function getCostBars(costComparison: PtuRecommendationCostComparison) {
  return [
    { label: 'PAYGO', value: costComparison.paygoCostEstimate, fill: '#4fd1c5' },
    { label: 'PTU On-Demand', value: costComparison.ptuOnDemandMonthly, fill: '#60a5fa' },
    { label: 'PTU Monthly', value: costComparison.ptuMonthlyReserved, fill: '#a78bfa' },
    { label: 'PTU 1-Year', value: costComparison.ptuYearlyReserved, fill: '#f59e0b' },
    { label: 'Spillover', value: costComparison.spilloverEstimate, fill: '#fb7185' },
  ]
}

function getRecommendedCost(costComparison: PtuRecommendationCostComparison) {
  const recommendation = costComparison.recommendation.toLowerCase()

  if (recommendation.includes('paygo')) {
    return costComparison.paygoCostEstimate
  }

  if (recommendation.includes('on-demand')) {
    return costComparison.ptuOnDemandMonthly
  }

  if (recommendation.includes('1-year') || recommendation.includes('yearly')) {
    return costComparison.ptuYearlyReserved
  }

  if (recommendation.includes('monthly')) {
    return costComparison.ptuMonthlyReserved
  }

  if (recommendation.includes('spillover')) {
    return costComparison.spilloverEstimate
  }

  return Math.min(
    costComparison.paygoCostEstimate,
    costComparison.ptuOnDemandMonthly,
    costComparison.ptuMonthlyReserved,
    costComparison.ptuYearlyReserved,
  )
}

export function PtuCalculator() {
  const [days, setDays] = useState<(typeof DAY_OPTIONS)[number]>(30)
  const tpmRequest = useCallback(() => analyticsClient.getTpm(days), [days])
  const {
    data: tpm,
    loading,
    error,
    refresh,
  } = useApi(tpmRequest)

  const [overrides, setOverrides] = useState<Record<string, OverrideDraft>>({})
  const [recommendation, setRecommendation] = useState<PtuRecommendation | null>(null)
  const [isCalculating, setIsCalculating] = useState(false)
  const [calculationError, setCalculationError] = useState<string | null>(null)
  const [isDirty, setIsDirty] = useState(false)

  const orderedModels = useMemo(
    () => [...(tpm?.models ?? [])].sort((left, right) => right.p99Tpm - left.p99Tpm),
    [tpm],
  )

  useEffect(() => {
    setOverrides((current) => {
      const next: Record<string, OverrideDraft> = {}

      for (const model of orderedModels) {
        next[model.modelName] = current[model.modelName] ?? {
          inputRate: '',
          outputRate: '',
          tpmPerPtu: '',
          deploymentType: 'Global',
        }
      }

      return next
    })
  }, [orderedModels])

  useEffect(() => {
    setRecommendation(null)
    setCalculationError(null)
    setIsDirty(false)
  }, [days])

  const requestPayload = useMemo(
    () => buildRequest(days, orderedModels, overrides),
    [days, orderedModels, overrides],
  )
  const hasInvalidInputs = orderedModels.some((model) => {
    const override = overrides[model.modelName]

    return (
      isInvalidNumericInput(override?.inputRate ?? '') ||
      isInvalidNumericInput(override?.outputRate ?? '') ||
      isInvalidNumericInput(override?.tpmPerPtu ?? '')
    )
  })

  const totalTokens = orderedModels.reduce((sum, model) => sum + model.totalTokens, 0)
  const hottestModel = orderedModels[0] ?? null
  const maxBurstModel = [...orderedModels].sort((left, right) => right.maxTpm - left.maxTpm)[0] ?? null
  const costBars = recommendation ? getCostBars(recommendation.costComparison) : []
  const savings = recommendation
    ? Math.max(
        0,
        recommendation.costComparison.paygoCostEstimate -
          getRecommendedCost(recommendation.costComparison),
      )
    : 0

  const updateOverride = (
    modelName: string,
    field: keyof OverrideDraft,
    value: string | DeploymentType,
  ) => {
    setOverrides((current) => ({
      ...current,
      [modelName]: {
        ...(current[modelName] ?? {
          inputRate: '',
          outputRate: '',
          tpmPerPtu: '',
          deploymentType: 'Global' as DeploymentType,
        }),
        [field]: value,
      },
    }))
    setIsDirty(true)
  }

  const handleCalculate = async () => {
    setIsCalculating(true)
    setCalculationError(null)

    try {
      const response = await analyticsClient.calculatePtu(requestPayload)
      setRecommendation(response)
      setIsDirty(false)
    } catch (calculateError) {
      setCalculationError(toErrorMessage(calculateError))
    } finally {
      setIsCalculating(false)
    }
  }

  if (loading && !tpm) {
    return (
      <section className="page">
        <header className="page-header">
          <div>
            <p className="page-eyebrow">PTU Calc</p>
            <h2 className="page-title">Reserved capacity sizing</h2>
          </div>
          <p className="page-note">Loading TPM baselines…</p>
        </header>
      </section>
    )
  }

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="page-eyebrow">PTU Calc</p>
          <h2 className="page-title">Reserved capacity sizing</h2>
        </div>
        <p className="page-note">
          {formatWholeNumber(tpm?.totalMinutesInWindow ?? 0)} observed minutes
          <br />
          {formatTokenCount(totalTokens)} in scope
        </p>
      </header>

      <div className="analytics-toolbar">
        <div className="segmented-control" role="tablist" aria-label="TPM window selector">
          {DAY_OPTIONS.map((option) => (
            <button
              key={option}
              type="button"
              className={option === days ? 'segmented-button is-active' : 'segmented-button'}
              onClick={() => setDays(option)}
            >
              {option}d
            </button>
          ))}
        </div>
        <button type="button" className="action-button" onClick={refresh}>
          Refresh
        </button>
      </div>

      {error ? (
        <Panel
          title="TPM feed issue"
          subtitle={error}
          aside={
            <button type="button" className="action-button" onClick={refresh}>
              Retry
            </button>
          }
        >
          <div className="empty-state">
            <p>TPM baselines will repopulate after the next successful analytics response.</p>
          </div>
        </Panel>
      ) : null}

      {calculationError ? (
        <Panel title="Calculation issue" subtitle={calculationError}>
          <div className="empty-state">
            <p>Adjust overrides or retry once the API is available.</p>
          </div>
        </Panel>
      ) : null}

      <section className="summary-grid" aria-label="PTU baseline summary">
        <article className="summary-card">
          <span className="summary-card-label">Models in scope</span>
          <strong className="summary-card-value">{formatWholeNumber(orderedModels.length)}</strong>
          <p className="summary-card-detail">Distinct model families with observed TPM data</p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Top P99 model</span>
          <strong className="summary-card-value">{hottestModel?.modelName ?? 'No data'}</strong>
          <p className="summary-card-detail mono">
            {formatCompactNumber(hottestModel?.p99Tpm ?? 0)} TPM p99
          </p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Max burst</span>
          <strong className="summary-card-value">{maxBurstModel?.modelName ?? 'No data'}</strong>
          <p className="summary-card-detail mono">
            {formatCompactNumber(maxBurstModel?.maxTpm ?? 0)} TPM max
          </p>
        </article>
        <article className="summary-card">
          <span className="summary-card-label">Window volume</span>
          <strong className="summary-card-value">{formatTokenCount(totalTokens)}</strong>
          <p className="summary-card-detail mono">Window {days}d baseline</p>
        </article>
      </section>

      <Panel
        title="TPM baseline"
        subtitle="Synced model pressure metrics that seed the PTU sizing request."
        aside={<span className="status-text mono">{formatWholeNumber(orderedModels.length)} models</span>}
      >
        {orderedModels.length === 0 ? (
          <div className="empty-state">
            <p>No TPM analytics are available yet. Run a sync and retry.</p>
          </div>
        ) : (
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Model</th>
                  <th className="mono">Total tokens</th>
                  <th className="mono">Avg TPM</th>
                  <th className="mono">P95 TPM</th>
                  <th className="mono">P99 TPM</th>
                  <th className="mono">Max TPM</th>
                </tr>
              </thead>
              <tbody>
                {orderedModels.map((model) => (
                  <tr key={model.modelName}>
                    <td>{model.modelName}</td>
                    <td className="mono">{formatTokenCount(model.totalTokens)}</td>
                    <td className="mono">{formatCompactNumber(model.avgTpm)}</td>
                    <td className="mono">{formatCompactNumber(model.p95Tpm)}</td>
                    <td className="mono">{formatCompactNumber(model.p99Tpm)}</td>
                    <td className="mono">{formatCompactNumber(model.maxTpm)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      <Panel
        title="Rate overrides"
        subtitle="Blank rate fields defer to API defaults. Override only when you have contract-specific pricing or TPM/PTU ratios."
        aside={
          <button
            type="button"
            className="action-button"
            onClick={handleCalculate}
            disabled={isCalculating || hasInvalidInputs || orderedModels.length === 0}
          >
            {isCalculating ? 'Calculating…' : 'Calculate'}
          </button>
        }
      >
        {orderedModels.length === 0 ? (
          <div className="empty-state">
            <p>No model baselines are available to size against.</p>
          </div>
        ) : (
          <>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Model</th>
                    <th className="mono">Input $ / 1M</th>
                    <th className="mono">Output $ / 1M</th>
                    <th className="mono">TPM / PTU</th>
                    <th>Deployment type</th>
                  </tr>
                </thead>
                <tbody>
                  {orderedModels.map((model) => {
                    const override = overrides[model.modelName] ?? {
                      inputRate: '',
                      outputRate: '',
                      tpmPerPtu: '',
                      deploymentType: 'Global' as DeploymentType,
                    }

                    return (
                      <tr key={model.modelName}>
                        <td>
                          <div className="table-cell-stack">
                            <span className="item-title">{model.modelName}</span>
                            <span className="table-secondary mono">
                              p99 {formatCompactNumber(model.p99Tpm)} TPM
                            </span>
                          </div>
                        </td>
                        <td>
                          <input
                            type="number"
                            inputMode="decimal"
                            min="0"
                            step="0.01"
                            className="field-input mono"
                            value={override.inputRate}
                            placeholder="auto"
                            onChange={(event) =>
                              updateOverride(model.modelName, 'inputRate', event.target.value)
                            }
                          />
                        </td>
                        <td>
                          <input
                            type="number"
                            inputMode="decimal"
                            min="0"
                            step="0.01"
                            className="field-input mono"
                            value={override.outputRate}
                            placeholder="auto"
                            onChange={(event) =>
                              updateOverride(model.modelName, 'outputRate', event.target.value)
                            }
                          />
                        </td>
                        <td>
                          <input
                            type="number"
                            inputMode="decimal"
                            min="0"
                            step="1"
                            className="field-input mono"
                            value={override.tpmPerPtu}
                            placeholder="auto"
                            onChange={(event) =>
                              updateOverride(model.modelName, 'tpmPerPtu', event.target.value)
                            }
                          />
                        </td>
                        <td>
                          <select
                            className="field-select"
                            value={override.deploymentType}
                            onChange={(event) =>
                              updateOverride(
                                model.modelName,
                                'deploymentType',
                                event.target.value as DeploymentType,
                              )
                            }
                          >
                            {DEPLOYMENT_TYPES.map((deploymentType) => (
                              <option key={deploymentType} value={deploymentType}>
                                {deploymentType}
                              </option>
                            ))}
                          </select>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
            <p className="helper-text">
              {hasInvalidInputs
                ? 'Fix invalid numeric overrides before calculating.'
                : isDirty && recommendation
                  ? 'Overrides changed. Re-run calculation to refresh the recommendation.'
                  : 'The request posts current TPM baselines plus any explicit rate overrides.'}
            </p>
          </>
        )}
      </Panel>

      <Panel
        title="Sizing output"
        subtitle="Per-model PTU guidance after applying the current override set."
        aside={
          recommendation ? (
            <span className="status-text mono">
              {formatWholeNumber(recommendation.models.length)} recommendations
            </span>
          ) : undefined
        }
      >
        {!recommendation ? (
          <div className="empty-state">
            <p>Run the calculator to see PTU counts, utilization, and the cost comparison spread.</p>
          </div>
        ) : (
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Model</th>
                  <th className="mono">Avg TPM</th>
                  <th className="mono">P99 TPM</th>
                  <th className="mono">TPM / PTU</th>
                  <th className="mono">Recommended PTUs</th>
                  <th className="mono">Minimum PTUs</th>
                  <th className="mono">Utilization</th>
                </tr>
              </thead>
              <tbody>
                {recommendation.models.map((model) => (
                  <tr key={model.modelName}>
                    <td>{model.modelName}</td>
                    <td className="mono">{formatCompactNumber(model.avgTpm)}</td>
                    <td className="mono">{formatCompactNumber(model.p99Tpm)}</td>
                    <td className="mono">{formatCompactNumber(model.tpmPerPtu)}</td>
                    <td className="mono">{formatWholeNumber(model.recommendedPtus)}</td>
                    <td className="mono">{formatWholeNumber(model.minimumPtus)}</td>
                    <td className="mono">{formatUtilization(model.utilizationAtRecommended)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      <Panel title="Cost comparison" subtitle="Monthly cost spread across PAYGO and PTU purchase modes.">
        {!recommendation ? (
          <div className="empty-state">
            <p>Cost comparison will appear after the first successful calculation.</p>
          </div>
        ) : (
          <>
            <div className="panel-callout">
              <span className="summary-card-label">Recommendation</span>
              <strong>{recommendation.costComparison.recommendation}</strong>
              <p className="mono">Potential savings vs PAYGO: {formatCurrency(savings)}</p>
            </div>

            <div className="cost-grid">
              <article className="cost-card">
                <span className="cost-card-label">PAYGO</span>
                <strong className="cost-card-value mono">
                  {formatCurrency(recommendation.costComparison.paygoCostEstimate)}
                </strong>
              </article>
              <article className="cost-card">
                <span className="cost-card-label">PTU On-Demand</span>
                <strong className="cost-card-value mono">
                  {formatCurrency(recommendation.costComparison.ptuOnDemandMonthly)}
                </strong>
              </article>
              <article className="cost-card">
                <span className="cost-card-label">PTU Monthly Reserved</span>
                <strong className="cost-card-value mono">
                  {formatCurrency(recommendation.costComparison.ptuMonthlyReserved)}
                </strong>
              </article>
              <article className="cost-card">
                <span className="cost-card-label">PTU 1-Year Reserved</span>
                <strong className="cost-card-value mono">
                  {formatCurrency(recommendation.costComparison.ptuYearlyReserved)}
                </strong>
              </article>
              <article className="cost-card">
                <span className="cost-card-label">Spillover</span>
                <strong className="cost-card-value mono">
                  {formatCurrency(recommendation.costComparison.spilloverEstimate)}
                </strong>
              </article>
            </div>

            <div className="chart-wrap is-compact">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={costBars} margin={{ top: 8, right: 16, bottom: 8, left: 16 }}>
                  <CartesianGrid stroke="#223047" strokeDasharray="3 3" />
                  <XAxis
                    dataKey="label"
                    stroke="#94a3b8"
                    interval={0}
                    angle={-15}
                    textAnchor="end"
                    height={68}
                  />
                  <YAxis
                    tickFormatter={(value) => formatCurrencyAmount(Number(value))}
                    stroke="#94a3b8"
                    width={90}
                  />
                  <Tooltip
                    formatter={(value) => [formatCurrency(Number(value)), 'Estimated monthly cost']}
                    contentStyle={{
                      backgroundColor: '#0f1724',
                      border: '1px solid #223047',
                      borderRadius: '12px',
                    }}
                  />
                  <Bar dataKey="value" radius={[6, 6, 0, 0]}>
                    {costBars.map((entry) => (
                      <Cell key={entry.label} fill={entry.fill} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          </>
        )}
      </Panel>
    </section>
  )
}
