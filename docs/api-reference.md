---
title: API Reference
---

# API reference

[Back to docs home](index.md)

## Conventions

- Base API group: `/api`
- Authentication: cookie session established through `/auth/login`
- JSON casing: camelCase
- Dates in query strings: `yyyy-MM-dd`
- Analytics window values: `30`, `60`, or `90` only

## Authentication behavior

- `/auth/login` and `/auth/logout` are public browser endpoints.
- `/auth/me` returns JSON and is used by the SPA to check the current session.
- Every `/api/*` endpoint requires the auth cookie.
- Unauthenticated `/api/*` and `/auth/me` requests return `401` instead of redirecting.
- Forbidden `/api/*` requests return `403`.

## Common error responses

### `401 Unauthorized`

Returned when the auth cookie is missing or expired.

### `403 Forbidden`

Returned when the current authenticated user is denied access.

### `400 ValidationProblem`

Example for an invalid analytics window:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "days": [
      "days must be one of 30, 60, or 90."
    ]
  }
}
```

Example for an invalid date range:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "dateRange": [
      "endDate must be on or after startDate."
    ]
  }
}
```

## Auth endpoints

### `GET /auth/login`

- **Authentication:** public
- **Query parameters:** none
- **Request body:** none
- **Response:** redirect to `/` if already authenticated; otherwise starts the Microsoft Entra OIDC challenge
- **Errors:** not documented as JSON; browser flow endpoint

### `GET /auth/logout`

- **Authentication:** public
- **Query parameters:** none
- **Request body:** none
- **Response:** signs out the auth cookie and OIDC session, then redirects to `/`
- **Errors:** not documented as JSON; browser flow endpoint

### `GET /auth/me`

- **Authentication:** returns `401` when no session is present
- **Query parameters:** none
- **Request body:** none
- **Response schema:**

| Field | Type |
|---|---|
| `name` | string |
| `email` | string |

**Example `200 OK`**

```json
{
  "name": "Adele Vance",
  "email": "adele.vance@contoso.com"
}
```

## Billing endpoints

### `GET /api/billing/metrics`

- **Authentication:** required
- **Query parameters:**

| Name | Type | Required | Notes |
|---|---|---|---|
| `startDate` | `yyyy-MM-dd` | No | Inclusive |
| `endDate` | `yyyy-MM-dd` | No | Inclusive day range; API converts internally to next-day exclusive bound |

- **Response schema:** array of `BillingMetricResponse`

| Field | Type |
|---|---|
| `deploymentName` | string |
| `modelName` | string |
| `modelVersion` | string or null |
| `hubName` | string |
| `timestamp` | ISO 8601 datetime |
| `promptTokens` | integer |
| `completionTokens` | integer |
| `totalTokens` | integer |

**Example `200 OK`**

```json
[
  {
    "deploymentName": "gpt4o-prod-eastus",
    "modelName": "gpt-4o",
    "modelVersion": "2024-11-20",
    "hubName": "fb-prod-eastus",
    "timestamp": "2026-06-24T14:00:00+00:00",
    "promptTokens": 145220,
    "completionTokens": 60211,
    "totalTokens": 205431
  },
  {
    "deploymentName": "gpt4o-mini-batch",
    "modelName": "gpt-4o-mini",
    "modelVersion": "2024-07-18",
    "hubName": "fb-prod-eastus",
    "timestamp": "2026-06-24T15:00:00+00:00",
    "promptTokens": 82444,
    "completionTokens": 25101,
    "totalTokens": 107545
  }
]
```

- **Errors:** `400` for invalid date range, `401`, `403`

### `GET /api/billing/summary`

- **Authentication:** required
- **Query parameters:** same as `/api/billing/metrics`
- **Response schema:**

| Field | Type |
|---|---|
| `hubCount` | integer |
| `projectCount` | integer |
| `deploymentCount` | integer |
| `totalPromptTokens` | integer |
| `totalCompletionTokens` | integer |
| `totalTokens` | integer |
| `oldestMetric` | ISO 8601 datetime or null |
| `newestMetric` | ISO 8601 datetime or null |
| `byModel` | array of model breakdown objects |

Model breakdown object:

| Field | Type |
|---|---|
| `modelName` | string |
| `promptTokens` | integer |
| `completionTokens` | integer |
| `totalTokens` | integer |

**Example `200 OK`**

```json
{
  "hubCount": 2,
  "projectCount": 5,
  "deploymentCount": 7,
  "totalPromptTokens": 4852100,
  "totalCompletionTokens": 1794450,
  "totalTokens": 6646550,
  "oldestMetric": "2026-05-27T00:00:00+00:00",
  "newestMetric": "2026-06-25T08:00:00+00:00",
  "byModel": [
    {
      "modelName": "gpt-4o",
      "promptTokens": 3124100,
      "completionTokens": 1202500,
      "totalTokens": 4326600
    },
    {
      "modelName": "gpt-4o-mini",
      "promptTokens": 1728000,
      "completionTokens": 591950,
      "totalTokens": 2329950
    }
  ]
}
```

- **Errors:** `400` for invalid date range, `401`, `403`

## Inventory endpoints

### `GET /api/hubs`

- **Authentication:** required
- **Query parameters:** none
- **Response schema:** array of `FoundryHubResponse`

| Field | Type |
|---|---|
| `id` | GUID |
| `name` | string |
| `region` | string |
| `subscriptionId` | string |
| `deploymentCount` | integer |
| `projectCount` | integer |
| `lastSyncedAt` | ISO 8601 datetime or null |

**Example `200 OK`**

```json
[
  {
    "id": "1fa67133-0e9b-4f2d-a28f-12279cd81c3d",
    "name": "fb-prod-eastus",
    "region": "eastus",
    "subscriptionId": "11111111-2222-3333-4444-555555555555",
    "deploymentCount": 4,
    "projectCount": 3,
    "lastSyncedAt": "2026-06-25T08:00:02+00:00"
  },
  {
    "id": "3a108443-2ad1-47ca-a246-1e1f7f7457b1",
    "name": "fb-prod-sweden",
    "region": "swedencentral",
    "subscriptionId": "11111111-2222-3333-4444-555555555555",
    "deploymentCount": 3,
    "projectCount": 2,
    "lastSyncedAt": "2026-06-25T08:01:11+00:00"
  }
]
```

### `GET /api/projects`

- **Authentication:** required
- **Query parameters:** none
- **Response schema:** array of `FoundryProjectResponse`

| Field | Type |
|---|---|
| `id` | GUID |
| `name` | string |
| `hubName` | string |
| `region` | string |
| `lastSyncedAt` | ISO 8601 datetime or null |

**Example `200 OK`**

```json
[
  {
    "id": "0ef86098-9f62-4fbb-a9aa-4372f1b7f37c",
    "name": "customer-support",
    "hubName": "fb-prod-eastus",
    "region": "eastus",
    "lastSyncedAt": "2026-06-25T08:00:04+00:00"
  },
  {
    "id": "5801ef39-c818-4a2f-b967-03926c894a4a",
    "name": "internal-assistants",
    "hubName": "fb-prod-sweden",
    "region": "swedencentral",
    "lastSyncedAt": "2026-06-25T08:01:14+00:00"
  }
]
```

### `GET /api/projects/{projectId}`

- **Authentication:** required
- **Route parameters:** `projectId: Guid`
- **Response schema:** same as `GET /api/projects`

**Example `200 OK`**

```json
{
  "id": "0ef86098-9f62-4fbb-a9aa-4372f1b7f37c",
  "name": "customer-support",
  "hubName": "fb-prod-eastus",
  "region": "eastus",
  "lastSyncedAt": "2026-06-25T08:00:04+00:00"
}
```

- **Errors:** `404` if the project ID is not found, `401`, `403`

### `GET /api/deployments`

- **Authentication:** required
- **Query parameters:**

| Name | Type | Required | Notes |
|---|---|---|---|
| `hubId` | GUID | No | Filters to deployments owned by a hub |

- **Response schema:** array of `DeploymentResponse`

| Field | Type |
|---|---|
| `id` | GUID |
| `deploymentName` | string |
| `modelName` | string |
| `modelVersion` | string or null |
| `hubName` | string |
| `totalTokensLast24h` | integer |
| `lastMetricAt` | ISO 8601 datetime or null |

**Example `200 OK`**

```json
[
  {
    "id": "4521fd59-4be3-46a7-9374-877f0c116ea8",
    "deploymentName": "gpt4o-prod-eastus",
    "modelName": "gpt-4o",
    "modelVersion": "2024-11-20",
    "hubName": "fb-prod-eastus",
    "totalTokensLast24h": 1487440,
    "lastMetricAt": "2026-06-25T08:00:00+00:00"
  },
  {
    "id": "99b2cbf2-1d09-4b97-a095-ac3bb65e3248",
    "deploymentName": "o4mini-eval",
    "modelName": "o4-mini",
    "modelVersion": null,
    "hubName": "fb-prod-sweden",
    "totalTokensLast24h": 241990,
    "lastMetricAt": "2026-06-25T08:00:00+00:00"
  }
]
```

### `GET /api/agents`

- **Authentication:** required
- **Query parameters:**

| Name | Type | Required | Notes |
|---|---|---|---|
| `projectId` | GUID | No | Filters to a single project |
| `hubId` | GUID | No | Filters to agents whose project belongs to a hub |

- **Response schema:** array of `AgentResponse`

| Field | Type |
|---|---|
| `id` | GUID |
| `agentId` | string |
| `name` | string |
| `description` | string or null |
| `modelName` | string or null |
| `kind` | string or null |
| `projectName` | string |
| `hubName` | string |
| `createdAt` | ISO 8601 datetime or null |
| `lastSyncedAt` | ISO 8601 datetime or null |

**Example `200 OK`**

```json
[
  {
    "id": "ae98d86e-cbb7-4f63-aa3d-3b61f23b771c",
    "agentId": "agent_customer_triage",
    "name": "Customer Triage",
    "description": "Routes support tickets to the right workflow.",
    "modelName": "gpt-4o-mini",
    "kind": "Prompt",
    "projectName": "customer-support",
    "hubName": "fb-prod-eastus",
    "createdAt": "2026-06-01T12:15:00+00:00",
    "lastSyncedAt": "2026-06-25T08:00:05+00:00"
  },
  {
    "id": "5705750f-cb77-4c73-bf1f-ab20fdfb9dd1",
    "agentId": "agent_eval_runner",
    "name": "Eval Runner",
    "description": null,
    "modelName": "o4-mini",
    "kind": "Hosted",
    "projectName": "internal-assistants",
    "hubName": "fb-prod-sweden",
    "createdAt": "2026-05-29T08:40:00+00:00",
    "lastSyncedAt": "2026-06-25T08:01:15+00:00"
  }
]
```

## Analytics endpoints

### `GET /api/analytics/usage`

- **Authentication:** required
- **Query parameters:**

| Name | Type | Required | Allowed values |
|---|---|---|---|
| `days` | integer | No | `30`, `60`, `90` |

- **Response schema:**

| Field | Type |
|---|---|
| `days` | integer |
| `windowStart` | ISO 8601 datetime |
| `windowEnd` | ISO 8601 datetime |
| `totalPromptTokens` | integer |
| `totalCompletionTokens` | integer |
| `totalTokens` | integer |
| `dailyUsage` | array of `{ date, promptTokens, completionTokens, totalTokens }` |
| `byModel` | array of `{ modelName, promptTokens, completionTokens, totalTokens, deploymentCount }` |
| `byDeployment` | array of `{ deploymentName, modelName, hubName, promptTokens, completionTokens, totalTokens }` |

**Example `200 OK`**

```json
{
  "days": 30,
  "windowStart": "2026-05-27T00:00:00+00:00",
  "windowEnd": "2026-06-25T08:17:12+00:00",
  "totalPromptTokens": 4852100,
  "totalCompletionTokens": 1794450,
  "totalTokens": 6646550,
  "dailyUsage": [
    {
      "date": "2026-06-23",
      "promptTokens": 162300,
      "completionTokens": 60210,
      "totalTokens": 222510
    },
    {
      "date": "2026-06-24",
      "promptTokens": 175800,
      "completionTokens": 64050,
      "totalTokens": 239850
    }
  ],
  "byModel": [
    {
      "modelName": "gpt-4o",
      "promptTokens": 3124100,
      "completionTokens": 1202500,
      "totalTokens": 4326600,
      "deploymentCount": 2
    },
    {
      "modelName": "gpt-4o-mini",
      "promptTokens": 1728000,
      "completionTokens": 591950,
      "totalTokens": 2329950,
      "deploymentCount": 3
    }
  ],
  "byDeployment": [
    {
      "deploymentName": "gpt4o-prod-eastus",
      "modelName": "gpt-4o",
      "hubName": "fb-prod-eastus",
      "promptTokens": 2610000,
      "completionTokens": 991400,
      "totalTokens": 3601400
    },
    {
      "deploymentName": "gpt4o-mini-batch",
      "modelName": "gpt-4o-mini",
      "hubName": "fb-prod-eastus",
      "promptTokens": 1200200,
      "completionTokens": 420100,
      "totalTokens": 1620300
    }
  ]
}
```

- **Errors:** `400` for invalid `days`, `401`, `403`

### `GET /api/analytics/tpm`

- **Authentication:** required
- **Query parameters:** same as `/api/analytics/usage`
- **Response schema:**

| Field | Type |
|---|---|
| `days` | integer |
| `totalMinutesInWindow` | integer |
| `models` | array of `{ modelName, totalTokens, avgTpm, p95Tpm, p99Tpm, maxTpm }` |

**Example `200 OK`**

```json
{
  "days": 30,
  "totalMinutesInWindow": 41857,
  "models": [
    {
      "modelName": "gpt-4o",
      "totalTokens": 4326600,
      "avgTpm": 103.36,
      "p95Tpm": 918.42,
      "p99Tpm": 1420.33,
      "maxTpm": 1814.2
    },
    {
      "modelName": "gpt-4o-mini",
      "totalTokens": 2329950,
      "avgTpm": 55.66,
      "p95Tpm": 440.17,
      "p99Tpm": 702.55,
      "maxTpm": 980.5
    }
  ]
}
```

- **Errors:** `400` for invalid `days`, `401`, `403`

### `POST /api/analytics/ptu-recommendation`

- **Authentication:** required
- **Query parameters:** none
- **Request schema:**

| Field | Type | Required | Notes |
|---|---|---|---|
| `days` | integer | No | Defaults to `30`; allowed `30`, `60`, `90` |
| `customInputRates` | object | No | Dictionary of `modelName -> USD per 1M prompt tokens` |
| `customOutputRates` | object | No | Dictionary of `modelName -> USD per 1M completion tokens` |
| `customTpmPerPtu` | object | No | Dictionary of `modelName -> TPM capacity per PTU` |
| `deploymentType` | string | No | Defaults to `Global`; allowed `Global`, `DataZone`, `Regional` |

**Example request**

```json
{
  "days": 30,
  "customInputRates": {
    "gpt-4o": 2.25,
    "gpt-4o-mini": 0.14
  },
  "customOutputRates": {
    "gpt-4o": 9.5,
    "gpt-4o-mini": 0.55
  },
  "customTpmPerPtu": {
    "gpt-4o": 3000,
    "gpt-4o-mini": 12000
  },
  "deploymentType": "Global"
}
```

> Direct API callers should use the dictionary-based server contract above. The frontend has additional client-side types, but the server only binds the fields shown here.

- **Response schema:**

| Field | Type |
|---|---|
| `models` | array of model recommendation objects |
| `costComparison` | cost comparison object |

Model recommendation object:

| Field | Type |
|---|---|
| `modelName` | string |
| `avgTpm` | number |
| `p99Tpm` | number |
| `tpmPerPtu` | integer |
| `recommendedPtus` | integer |
| `minimumPtus` | integer |
| `utilizationAtRecommended` | number |

Cost comparison object:

| Field | Type |
|---|---|
| `paygoCostEstimate` | decimal |
| `ptuOnDemandMonthly` | decimal |
| `ptuMonthlyReserved` | decimal |
| `ptuYearlyReserved` | decimal |
| `spilloverEstimate` | decimal |
| `recommendation` | string |

**Example `200 OK`**

```json
{
  "models": [
    {
      "modelName": "gpt-4o",
      "avgTpm": 103.36,
      "p99Tpm": 1420.33,
      "tpmPerPtu": 3000,
      "recommendedPtus": 1,
      "minimumPtus": 1,
      "utilizationAtRecommended": 0.0345
    },
    {
      "modelName": "gpt-4o-mini",
      "avgTpm": 55.66,
      "p99Tpm": 702.55,
      "tpmPerPtu": 12000,
      "recommendedPtus": 1,
      "minimumPtus": 1,
      "utilizationAtRecommended": 0.0046
    }
  ],
  "costComparison": {
    "paygoCostEstimate": 16.78,
    "ptuOnDemandMonthly": 2880.0,
    "ptuMonthlyReserved": 1036.8,
    "ptuYearlyReserved": 864.0,
    "spilloverEstimate": 1036.8,
    "recommendation": "PAYGO"
  }
}
```

- **Errors:** `400` for invalid `days` or `deploymentType`, `401`, `403`

## Sync endpoints

### `POST /api/sync/trigger`

- **Authentication:** required
- **Query parameters:** none
- **Request body:** none
- **Response schema:**

| Field | Type |
|---|---|
| `runId` | GUID |

**Example `202 Accepted`**

Headers:

```text
Location: /api/sync/history
```

Body:

```json
{
  "runId": "8df1a366-2207-4ff2-90fd-d8565ca678e2"
}
```

- **Errors:** `401`, `403`

### `GET /api/sync/status`

- **Authentication:** required
- **Query parameters:** none
- **Response schema:**

| Field | Type |
|---|---|
| `isRunning` | boolean |
| `currentRun` | `{ id, startedAt, status }` or null |
| `lastCompletedAt` | ISO 8601 datetime or null |

**Example `200 OK`**

```json
{
  "isRunning": true,
  "currentRun": {
    "id": "8df1a366-2207-4ff2-90fd-d8565ca678e2",
    "startedAt": "2026-06-25T08:20:00+00:00",
    "status": "Running"
  },
  "lastCompletedAt": "2026-06-25T07:00:43+00:00"
}
```

### `GET /api/sync/history`

- **Authentication:** required
- **Query parameters:** none
- **Response schema:** object containing `runs`, limited to the 20 newest runs

Run object fields:

| Field | Type |
|---|---|
| `id` | GUID |
| `startedAt` | ISO 8601 datetime |
| `completedAt` | ISO 8601 datetime or null |
| `status` | string |
| `errorMessage` | string or null |
| `hubsDiscovered` | integer |
| `projectsDiscovered` | integer |
| `deploymentsDiscovered` | integer |
| `agentsDiscovered` | integer |
| `usageSlicesInserted` | integer |

**Example `200 OK`**

```json
{
  "runs": [
    {
      "id": "8df1a366-2207-4ff2-90fd-d8565ca678e2",
      "startedAt": "2026-06-25T08:20:00+00:00",
      "completedAt": "2026-06-25T08:20:14+00:00",
      "status": "Completed",
      "errorMessage": null,
      "hubsDiscovered": 2,
      "projectsDiscovered": 5,
      "deploymentsDiscovered": 7,
      "agentsDiscovered": 9,
      "usageSlicesInserted": 14
    },
    {
      "id": "21b39d2a-a6b9-4c79-bebc-0d9e73f275fe",
      "startedAt": "2026-06-25T07:00:30+00:00",
      "completedAt": "2026-06-25T07:00:43+00:00",
      "status": "Failed",
      "errorMessage": "1 hub sync operation(s) failed. See logs for details.",
      "hubsDiscovered": 2,
      "projectsDiscovered": 5,
      "deploymentsDiscovered": 7,
      "agentsDiscovered": 6,
      "usageSlicesInserted": 11
    }
  ]
}
```

- **Errors:** `401`, `403`

## Health endpoints

### `GET /health`

- **Authentication:** public
- **Purpose:** readiness probe used by Container Apps
- **Response:** health-check status code; body is not part of the stable application contract

### `GET /alive`

- **Authentication:** public
- **Purpose:** liveness probe used by Container Apps
- **Response:** health-check status code; body is not part of the stable application contract

## Development-only endpoint

### `GET /openapi/v1.json`

- **Authentication:** public in Development only
- **Query parameters:** none
- **Request body:** none
- **Response:** generated OpenAPI JSON document for the current Minimal API surface
- **Errors:** unavailable outside Development because `MapOpenApi()` is only enabled there
