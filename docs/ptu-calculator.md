---
title: PTU Calculator
---

# PTU calculator

[Back to docs home](index.md)

## What PTU means in this project

PTU stands for **Provisioned Throughput Unit**. In Azure AI Foundry / Azure OpenAI pricing, PTUs represent reserved throughput capacity for a deployment. Foundry Billing uses synced token usage to answer two practical questions:

1. How much throughput does each model family actually need?
2. Is the observed workload cheaper on PAYGO, reserved PTUs, or a mixed spillover strategy?

## Input data

The calculator does not read live Azure pricing or live Azure usage directly. It uses the data already synced into PostgreSQL.

The flow is:

1. `MetricsSyncWorker` writes hourly `UsageMetricSlice` rows.
2. `GET /api/analytics/tpm` loads those slices for a 30, 60, or 90 day window.
3. The analytics endpoint groups slices by model and converts them into TPM distributions.
4. `PtuCalculatorService` sizes PTUs and estimates costs from those derived per-model inputs.

## TPM calculation

### Window selection

The supported windows are fixed:

- `30` days
- `60` days
- `90` days

Any other value returns a validation error.

### Hourly buckets

The analytics code uses 60-minute TPM buckets.

For each `UsageMetricSlice`:

```text
slice TPM = totalTokens / intervalMinutes
```

Because the sync worker stores hourly slices, `intervalMinutes` is currently `60` for all inserted rows.

### Derived metrics per model

For each model family, the calculator receives:

- `totalTokens`
- `promptTokens`
- `completionTokens`
- `avgTpm`
- `p95Tpm`
- `p99Tpm`
- `maxTpm`
- full hourly TPM bucket list

`p95` and `p99` are calculated from sorted bucket values using linear interpolation.

## Built-in defaults

### Default TPM capacity per PTU

| Model key | TPM per PTU |
|---|---:|
| `gpt-4o` | 2,500 |
| `gpt-4o-mini` | 10,000 |
| `gpt-4.1` | 3,000 |
| `gpt-4.1-mini` | 10,000 |
| `gpt-4.1-nano` | 20,000 |
| `o3-mini` | 1,000 |
| `o3` | 500 |
| `o4-mini` | 5,000 |
| fallback | 2,500 |

### Default PAYGO rates (USD per 1M tokens)

| Model key | Input | Output |
|---|---:|---:|
| `gpt-4o` | 2.50 | 10.00 |
| `gpt-4o-mini` | 0.15 | 0.60 |
| `gpt-4.1` | 2.00 | 8.00 |
| `gpt-4.1-mini` | 0.40 | 1.60 |
| `gpt-4.1-nano` | 0.10 | 0.40 |
| `o3-mini` | 1.10 | 4.40 |
| `o3` | 2.00 | 8.00 |
| `o4-mini` | 1.10 | 4.40 |
| fallback | 2.50 | 10.00 |

### Deployment pricing profiles

| Deployment type | PAYGO multiplier | PTU on-demand $/hr | PTU monthly reserved $/hr | PTU yearly reserved $/hr |
|---|---:|---:|---:|---:|
| `Global` | 1.00 | 2.00 | 0.72 | 0.60 |
| `DataZone` | 1.10 | 2.20 | 0.79 | 0.66 |
| `Regional` | 1.20 | 2.40 | 0.86 | 0.72 |

## Model key matching

The service normalizes model names by:

- trimming whitespace
- converting `_` to `-`
- lower-casing
- matching known defaults by longest prefix

Example: `gpt-4o-2024-11-20` resolves to the `gpt-4o` default table entry.

Unknown model names fall back silently to the generic defaults.

## Rate overrides

The API accepts three override dictionaries keyed by model name:

- `customInputRates`
- `customOutputRates`
- `customTpmPerPtu`

Example request body:

```json
{
  "days": 60,
  "customInputRates": {
    "gpt-4o": 2.2
  },
  "customOutputRates": {
    "gpt-4o": 9.1
  },
  "customTpmPerPtu": {
    "gpt-4o": 3200
  },
  "deploymentType": "Regional"
}
```

Override resolution order is:

1. request override
2. known model default
3. generic fallback

## Recommendation logic

### Per-model PTU sizing

For each model:

- `recommendedPtus = ceil(p99Tpm / tpmPerPtu)` when the model has traffic
- `minimumPtus = ceil(avgTpm / tpmPerPtu)` when the model has traffic
- `utilizationAtRecommended = avgTpm / (recommendedPtus * tpmPerPtu)`

Interpretation:

- **Recommended PTUs** size for burst resistance at the model's `p99` demand level.
- **Minimum PTUs** size for the average steady-state load.
- **Utilization at recommended** shows how heavily the recommended reserved capacity would be used on average.

### PAYGO estimate

```text
prompt cost     = promptTokens / 1,000,000 * inputRate
completion cost = completionTokens / 1,000,000 * outputRate
total PAYGO     = prompt cost + completion cost
```

The deployment type multiplier is applied unless you override rates explicitly.

### Spillover estimate

Spillover is the hybrid strategy in the current implementation:

- reserve only the **minimum PTUs** needed for average load
- price the burst above that capacity at PAYGO rates

Burst tokens are derived bucket-by-bucket:

```text
overflow tokens in a bucket = max(0, bucketTpm - minimumCapacityTpm) * bucketDurationMinutes
```

The overflow is split into prompt and completion tokens using the observed prompt/completion ratio for that model.

### Aggregate monthly comparison

After per-model sizing, the service computes:

- `paygoCostEstimate` — current window cost, not monthly-scaled
- `ptuOnDemandMonthly`
- `ptuMonthlyReserved`
- `ptuYearlyReserved`
- `spilloverEstimate`

It also computes a monthly scale factor from the observed window length:

```text
monthlyScale = (24 * 30 * 60) / totalMinutesInWindow
```

That scaling is used internally for the recommendation decision.

## Cost comparison tiers explained

The UI shows five cost lanes:

1. **PAYGO** — pure token-based pricing at the observed mix of prompt and completion tokens
2. **PTU On-Demand** — recommended PTUs at the profile's on-demand hourly PTU rate
3. **PTU Monthly Reserved** — recommended PTUs at the monthly reserved hourly PTU rate
4. **PTU 1-Year Reserved** — recommended PTUs at the yearly reserved hourly PTU rate
5. **Spillover** — minimum PTUs at the monthly reserved rate plus PAYGO for burst traffic above that floor

## Final recommendation decision

The service returns one of these recommendation strings:

- `PAYGO`
- `PTU_MONTHLY`
- `PTU_YEARLY`
- `SPILLOVER`

The decision tree is:

```text
if PAYGO monthly equivalent <= 0          => PAYGO
else if PAYGO monthly equivalent < monthly reserved PTU => PAYGO
else if utilization > 70% and yearly PTU < PAYGO => PTU_YEARLY
else if utilization > 50% and spillover < both PAYGO and monthly PTU => SPILLOVER
else if utilization > 50% => PTU_MONTHLY
else => PAYGO
```

## Operational caveats

- The rate tables are hard-coded in `PtuCalculatorService`; there is no external pricing feed.
- The server accepts deployment types `Global`, `DataZone`, and `Regional`. If you call the API directly, use those exact values.
- The calculator uses synced usage history only. If the sync worker has only collected a few hours or days of data, the recommendation is based on that smaller sample.
- The calculator works at the **model family** level, not at a separate row per deployment.
