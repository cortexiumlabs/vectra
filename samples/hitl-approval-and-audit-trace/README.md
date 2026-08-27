# HITL Approval, Replay, and Audit Traceability

## Overview

This sample demonstrates Synentra's full human-in-the-loop governance lifecycle for a risky action:

1. request is authenticated and routed through `/proxy`
2. policy returns `REVIEW` (`HITL`)
3. request is suspended and listed in `/Hitls`
4. reviewer approves it
5. Synentra replays the original request upstream
6. audit evidence and correlated request logs are retained

## What this sample demonstrates

- Agent registration and JWT-based gateway authentication
- Reverse proxy governance via `/proxy/http://...`
- `ALLOW` path for safe reads
- `REVIEW` path for transfer creation
- HITL notification delivery through generic webhook notifier
- HITL status polling and reviewer decision APIs
- Replay behavior after approval
- Audit and request-correlation evidence (`X-Request-Id`, structured logs, SQLite)

## Scenario

`payments-agent` can read balances directly, but creating transfers requires a human decision before execution.

## Prerequisites

- Docker with Compose v2
- `curl`

## Folder/file structure

```text
hitl-approval-and-audit-trace/
  compose.yml
  README.md
  config/
	appsettings.json
  policies/
	payment-hitl-policy.json
  upstream/
	mappings/
	  get-balances.json
	  post-transfers.json
  review-webhook/
	mappings/
	  hitl-notifications.json
  data/
```

## Configuration

Key sample settings:

- `Policy:DefaultProvider = Internal`
- policy directory mapped to `/app/policies`
- `HumanInTheLoop:Enabled = true`
- `HumanInTheLoop:Notifications:GenericWebhook` enabled to deliver HITL events to `review-webhook`
- SQLite persistence at `/data/synentra.db`

## Policy explanation

`payment-hitl-policy` rules:

- `POST /v1/transfers*` -> `Hitl` (requires reviewer)
- `GET /v1/balances*` -> `Allow`
- default -> `Deny`

## Governance workflow

```mermaid
flowchart TD
  A[Agent sends POST /proxy/http://upstream-api:8080/v1/transfers] --> B[Synentra auth + access checks]
  B --> C[Policy evaluation]
  C -->|Hitl| D[Suspend request + write pending HITL + notify webhook]
  D --> E[Reviewer calls /Hitls/{id}/approve]
  E --> F[Synentra replays original request upstream]
  F --> G[Upstream 201 response streamed back]
  G --> H[Audit records + structured request logs]
```

## Step-by-step instructions

### 1) Start the sample

```bash
docker compose up -d
```

### 2) Register agent and assign policy

```bash
curl -s -X POST http://localhost:7082/Agents \
  -H "Content-Type: application/json" \
  -d '{
	"name": "payments-agent",
	"ownerId": "finance-platform",
	"clientSecret": "payments-secret-001"
  }'
```

Save `agentId`, then assign policy:

```bash
curl -i -X PUT http://localhost:7082/Agents/<agentId>/policy \
  -H "Content-Type: application/json" \
  -d '{"policyName":"payment-hitl-policy"}'
```

### 3) Generate Synentra gateway token

```bash
curl -s -X POST http://localhost:7082/Tokens \
  -H "Content-Type: application/json" \
  -d '{
	"agentId": "<agentId>",
	"clientSecret": "payments-secret-001"
  }'
```

Save `accessToken`.

## Example requests

### A) Safe read (ALLOW)

```bash
curl -i http://localhost:7082/proxy/http://upstream-api:8080/v1/balances \
  -H "Synentra-Authorization: Bearer <accessToken>"
```

Expected: `200 OK` with JSON body from upstream.

### B) Risky transfer (REVIEW / HITL)

```bash
curl -i -X POST http://localhost:7082/proxy/http://upstream-api:8080/v1/transfers \
  -H "Synentra-Authorization: Bearer <accessToken>" \
  -H "Content-Type: application/json" \
  -d '{
	"fromAccount": "acc-101",
	"toAccount": "acc-202",
	"amount": 12000,
	"currency": "USD",
	"reason": "Vendor settlement"
  }'
```

Expected:

- `202 Accepted`
- `Location: /hitls/<id>`
- body: request pending approval

Extract `<id>` from `Location`.

### C) List pending reviews

```bash
curl -s http://localhost:7082/Hitls?page=1&pageSize=25
```

Expected: pending HITL item containing the transfer request context.

### D) Approve and replay

```bash
curl -i -X POST http://localhost:7082/Hitls/<id>/approve \
  -H "Content-Type: application/json" \
  -d '{"comment":"Approved by treasury reviewer"}'
```

Expected:

- upstream replay result returned (`201 Created`)
- response body from upstream transfer API (`transferId`, `status`)

### E) Deny alternative

Instead of approval:

```bash
curl -i -X POST http://localhost:7082/Hitls/<id>/deny \
  -H "Content-Type: application/json" \
  -d '{"comment":"Insufficient justification"}'
```

Expected: `204 No Content` and no upstream execution.

## Expected decisions/results

- `GET /v1/balances` -> `ALLOW`
- `POST /v1/transfers` -> `REVIEW` (`HITL`) before execution
- `POST /Hitls/<id>/approve` -> replay executes upstream request
- `POST /Hitls/<id>/deny` -> request remains blocked

## Audit / observability behavior

### Verify webhook notification receipt

```bash
docker compose logs review-webhook --tail 200
```

You should see `POST /hitl-notifications` requests from Synentra.

### Verify Synentra correlation and decision logs

```bash
docker compose logs synentra --tail 300
```

Look for fields such as `request_id`, `decision`, `decision_reason`, `target_url`, and `risk_score`.

### Verify persisted audit records

```bash
docker run --rm \
  -v "${PWD}/data:/data" \
  keinos/sqlite3:latest \
  sqlite3 /data/synentra.db \
  "SELECT Id, Action, Status, Reason, Timestamp FROM AuditLogs ORDER BY Id DESC LIMIT 20;"
```

Expected records include statuses like `PENDING_HITL`, `HITL_APPROVED`/`HITL_DENIED`, and `HITL_REPLAYED`.

## Request correlation and traceability

- Every response includes `X-Request-Id` generated by `RequestLoggingMiddleware`.
- Use `X-Request-Id` plus Synentra logs and audit rows to trace:
  - incoming governed request
  - decision type (`ALLOW`/`REVIEW`/`DENY`)
  - reviewer action
  - replay result

## Cleanup/reset

Stop services:

```bash
docker compose down
```

Full reset:

```bash
docker compose down -v --remove-orphans
rm -f ./data/synentra.db ./data/synentra.db-shm ./data/synentra.db-wal
```
