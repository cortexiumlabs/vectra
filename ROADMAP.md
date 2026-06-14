# Synentra Roadmap

**Synentra** is an intent-aware governance gateway for autonomous AI agents. This roadmap outlines the technical direction, major milestones, and release strategy. It is a living document and evolves with community feedback, enterprise requirements, and the cloud-native ecosystem.

## Legend
- ✅ **Completed**
- 🚧 **In Progress**
- 📅 **Planned**

## v1.9.0 – Planned (Q4 2026)
- 📅 Horizontal scaling guide (multi-replica production deployments)
- 📅 Kubernetes operator (policy as CRDs)
- 📅 Built-in Prometheus alerting rules
- 📅 Centralized federated policy management
- 📅 React-based dashboard (HITL, agents, policies, audit logs)

## v1.8.0 – Planned (Q3 2026)
- 📅 External LLM fallback (Azure OpenAI, Gemini, Ollama)
- 📅 Semantic caching (exact + embedding-based)
- 📅 Advanced policy conditions (intent, risk score, confidence)
- 📅 Policy visual editor (web UI)
- 📅 Agent grouping and policy assignment
- 📅 Python SDK for integrations

## v1.7.0 – Rebranding to Synentra (June 2026)
This release completes the project rename from **Vectra** to **Synentra** across the entire codebase, documentation, and infrastructure.

### Changes:
- ✅ Full rename from Vectra → Synentra (codebase, packages, CI/CD)																				
- ✅ Updated Docker, Helm, and GitHub workflows
- ✅ Refreshed documentation and branding assets
- ✅ Updated CLI output and metadata																				
- ✅ Removed legacy resource files
- ✅ Fixed repository links and issue templates

**Impact:** Unified identity aligned with CNCF readiness.

## v1.6.0 – Observability & Telemetry Expansion (June 2026)

### Observability
- ✅ Request logging middleware (full HTTP lifecycle tracking)
- ✅ OpenTelemetry integration (OTLP export)
- ✅ Centralized observability configuration
- ✅ Enriched logs with agent, risk score, and decision metadata

### Testing
- ✅ Middleware unit tests (proxy, logging, OPA provider)
- ✅ Expanded DecisionEngine test coverage
- ✅ CLI testability improvements
- ✅ Coverage configuration improvements

### Refactoring
- ✅ Modularized startup pipeline
- ✅ Improved Program.cs testability

**Impact:** Production-grade observability and debugging capability.

## v1.5.0 – Policy Simulation & System Consolidation (June 2026)

### Features
- ✅ Policy simulation endpoint (`/v1/policies/simulate`)
- ✅ Safe dry-run policy evaluation
- ✅ Integration of HITL notifications with quarantine workflows
- ✅ Removal of legacy notification configuration

### Improvements
- ✅ Improved clarity of internal dispatch logic
- ✅ Reduced duplication in HITL/quarantine systems
													  								  
**Impact:** Safer policy iteration and reduced production risk.

## v1.4.0 – Agent Quarantine System (June 2026)

### Features
- ✅ Agent quarantine mechanism (automatic or manual isolation)
- ✅ Configurable thresholds (trust score, violation rate)
- ✅ Time-based or permanent quarantine modes
- ✅ REST APIs for quarantine management
- ✅ Enforcement of policies for quarantined agents

### Refactoring
- ✅ Improved AgentRequestAccessService modularity
- ✅ Better separation of concerns and extensibility

**Impact:** Stronger system safety and automated risk containment.

## v1.3.0 – Multi-Channel HITL Notifications (June 2026)

### Features:
- ✅ Pluggable notification interface (`IHitlNotifier`)
- ✅ Multi-channel support: Slack, Teams, PagerDuty, webhooks
- ✅ Shared base notifier abstraction
- ✅ Optional persistence of notification records
- ✅ Removal of legacy single-webhook system

### Configuration
- ✅ Per-channel configuration in `appsettings.json`
- ✅ Runtime enable/disable of notification channels

**Impact:** Faster and more flexible HITL response workflows across enterprise tools.

## v1.2.0 – Architecture & Code Quality Improvements (May 2026)

### Refactoring
- ✅ HITL refactored to CQRS-based dispatcher model
- ✅ Cleaned proxy middleware dependencies
- ✅ Unified semantic provider API (`body` parameter standardization)
- ✅ Improved `Void` utility struct operators
- ✅ Removed redundant logging dependencies

### Platform Improvements
- ✅ Default cancellation token support across handlers
- ✅ Improved logging diagnostics with exception context
- ✅ Enhanced async failure handling patterns

### CI/CD
- ✅ Dependabot integration
- ✅ SonarCloud quality gates (conditional pipeline execution)
- ✅ Improved project metadata and package URLs

## v1.1.1 – Quality, Documentation & Test Expansion (May 2026)

### Testing & Reliability:
- ✅ Expanded unit test coverage (handlers, persistence, infrastructure)
- ✅ Added dedicated test projects for infrastructure and unit testing
- ✅ Enabled coverage tooling and internals visibility
- ✅ Cleanup of unused logging dependencies
- ✅ Standardized cancellation token usage
- ✅ Improved async error handling

### Documentation:
- ✅ Enhanced README (features, quick start, contributing, security)
- ✅ Added architecture diagrams and navigation improvements
- ✅ Published documentation site integration

### Security & Reliability:
- ✅ Agent authentication middleware now uses non‑cancelable token for critical operations
- ✅ Proper request cancellation handling in handlers
- ✅ Suppressed security warning for `Math.random()` usage in load test (clarified comment)

## v1.1.0 – Stability, Load Testing & Secrets Management (May 2026)

### Features & Improvements:
- ✅ HITL request limits to prevent abuse
- ✅ Webhook notifications (Slack, Teams, HTTP endpoints)
- ✅ Configurable ONNX provider toggle
- ✅ Pluggable secrets management (Vault, Azure Key Vault, AWS Secrets Manager, env vars)
- ✅ Kestrel tuning (timeouts, concurrency, connection limits)
- ✅ Load testing suite (k6, 1000 RPS simulation)
- ✅ Improved agent authentication middleware (robust cancellation handling)
- ✅ Enhanced request cancellation support
- ✅ Header filtering for proxy safety
- ✅ Launch settings and Kestrel refactoring improvements

### Quality:
- ✅ Fixed null/empty check for `policyName` in `EvaluateAsync`
- ✅ Improved error handling and logging consistency

## v1.0 – Core Governance Gateway (Q1 2026)

The initial stable release focused on governance, reliability, and developer experience.

### Features
- ✅ Agent registration with JWT-based authentication (short-lived tokens)
- ✅ Reverse proxy (YARP) with `/proxy/{**catch-all}` routing
- ✅ Deterministic policy engine (JSON rules: `eq`, `contains`, `regex`, `in`)
- ✅ Human-in-the-Loop (HITL) system: suspend, approve/deny, Redis queue, polling, replay, audit hooks
- ✅ Audit logging (PostgreSQL) and structured logging (Serilog)
- ✅ Prometheus metrics (latency, throughput, policy decisions)
- ✅ Per-agent rate limiting (requests/sec configuration)
- ✅ Per-host circuit breaker for upstream protection
- ✅ ONNX semantic provider (embedded or in-memory model loading)
- ✅ Extensible semantic provider abstraction (ONNX, external LLMs)
- ✅ Basic risk scoring (method, path, agent trust score)
- ✅ CLI tool (`synctl`) for agent, policy, HITL, and audit operations
- ✅ Admin OpenAPI (Swagger) endpoints
- ✅ Docker image and Helm chart
- ✅ Configuration enhancements (env overrides, relative paths, dev mode support)

### Quality & Security
- ✅ Unit and integration tests (>80% coverage)
- ✅ 2FA enabled for GitHub organization

## Backlog
- 📅 Federated policy management (central control plane)
- 📅 Agent behaviour fingerprinting (unsupervised anomaly detection)
- 📅 gRPC support (besides HTTP/1.1)
- 📅 Plugin system for custom policies (WebAssembly or external gRPC)
- 📅 SLI / SLO dashboards – built‑in Grafana dashboards

## Release Cadence
- Minor releases: every 2 months
- Patch releases: as needed (security & critical fixes)
- Long-term support: each major version supported for 12 months

*Last updated: June 2026*