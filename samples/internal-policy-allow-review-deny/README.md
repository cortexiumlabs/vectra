# Internal Policy ALLOW / REVIEW / DENY

## Overview

This sample demonstrates a complete Synentra governance path using the internal policy provider:

- agent registration and identity
- gateway JWT issuance
- governed routing through `/proxy/<upstream-url>`
- policy-driven `ALLOW`, `REVIEW` (HITL), and `DENY` outcomes
- audit and request correlation evidence

## What this sample demonstrates

- Internal policy loading from `/app/policies`
- Per-agent policy assignment
- Safe read requests forwarded to upstream API (`ALLOW`)
- Destructive customer requests suspended for review (`REVIEW`)
- Admin-route requests blocked before upstream (`DENY`)
- Audit evidence stored in SQLite and request correlation via `X-Request-Id`

## Scenario

An agent named `customer-ops-agent` is allowed to read customer data, must request human approval before deleting customers, and is forbidden from admin endpoints.

## Prerequisites

- Docker with Compose v2
- `curl`

## Folder/file structure

```text
internal-policy-allow-review-deny/
  compose.yml
  README.md
  config/
	appsettings.json
  policies/
	customer-governance.json
  upstream/
	mappings/
	  get-customers.json
	  delete-customers.json
	  get-admin-stats.json
  data/
```

## Configuration

`config/appsettings.json` configures:

- SQLite persistence at `/data/synentra.db`
- memory cache for HITL and risk cache
- internal policy provider with directory `/app/policies`
- gateway JWT issuance (`Security:AgentAuth:TokenIssuance`)
- HITL enabled

## Policy explanation

`policies/customer-governance.json` includes three ordered behaviors:

1. `deny-admin-endpoints` (`priority: 300`) denies `/v1/admin*`
2. `review-customer-delete` (`priority: 200`) sends `DELETE /v1/customers*` to HITL
3. `allow-customer-read` (`priority: 100`) allows `GET /v1/customers*`

Default outcome is `Deny`.

## Governance workflow

```mermaid
flowchart LR
  A[Agent request with Synentra-Authorization JWT] --> B[Synentra /proxy]
  B --> C[Agent auth + agent status check]
  C --> D[Policy evaluation]
  D -->|Allow| E[Forward to upstream API]
  D -->|Hitl| F[Suspend request and return 202 + /Hitls/{id}]
  D -->|Deny| G[Return 403]
  E --> H[Audit log + request log]
  F --> H
  G --> H
```

## Step-by-step instructions

### 1) Start the sample

```bash
docker compose up -d
```

### 2) Register an agent

```bash
curl -s -X POST http://localhost:7081/Agents \
  -H "Content-Type: application/json" \
  -d '{
	"name": "customer-ops-agent",
	"ownerId": "platform-team",
	"clientSecret": "sample-secret-001"
  }'
```

Save `agentId` from the response.

### 3) Assign the policy

```bash
curl -i -X PUT http://localhost:7081/Agents/<agentId>/policy \
  -H "Content-Type: application/json" \
  -d '{"policyName": "customer-governance"}'
```

Expected: `204 No Content`.

### 4) Mint a gateway JWT

```bash
curl -s -X POST http://localhost:7081/Tokens \
  -H "Content-Type: application/json" \
  -d '{
	"agentId": "<agentId>",
	"clientSecret": "sample-secret-001"
  }'
```

Save `accessToken` from the response.

## Example requests and expected decisions/results

### ALLOW example (safe read)

```bash
curl -i http://localhost:7081/proxy/http://upstream-api:8080/v1/customers \
  -H "Synentra-Authorization: Bearer <accessToken>"
```

Expected:

- HTTP `200`
- upstream JSON customer list returned
- `X-Request-Id` header present

### REVIEW example (destructive action)

```bash
curl -i -X DELETE http://localhost:7081/proxy/http://upstream-api:8080/v1/customers \
  -H "Synentra-Authorization: Bearer <accessToken>" \
  -H "Content-Type: application/json" \
  -d '{"instruction":"Delete inactive customers"}'
```

Expected:

- HTTP `202 Accepted`
- `Location: /hitls/<id>` header
- body indicates pending approval

### DENY example (admin route)

```bash
curl -i http://localhost:7081/proxy/http://upstream-api:8080/v1/admin/stats \
  -H "Synentra-Authorization: Bearer <accessToken>"
```

Expected:

- HTTP `403 Forbidden`
- reason: admin route denied by policy

## Audit and observability behavior

Synentra writes decision evidence into SQLite (`AuditLogs`) and emits structured request logs.

### Inspect the latest audit records

```bash
docker run --rm \
  -v "${PWD}/data:/data" \
  keinos/sqlite3:latest \
  sqlite3 /data/synentra.db \
  "SELECT Id, AgentId, Action, Status, RiskScore, Reason, Timestamp FROM AuditLogs ORDER BY Id DESC LIMIT 10;"
```

You should see rows with statuses such as `Allow`, `Deny`, and `PENDING_HITL`/HITL transition entries.

### Inspect request logs and correlation IDs

```bash
docker compose logs synentra --tail 200
```

Look for:

- `request_id` / `trace_id`
- `decision`
- `risk_score`
- `decision_reason`
- `target_url`

## Cleanup and reset

Stop services:

```bash
docker compose down
```

Full reset (remove generated DB and containers):

```bash
docker compose down -v --remove-orphans
rm -f ./data/synentra.db ./data/synentra.db-shm ./data/synentra.db-wal
```
