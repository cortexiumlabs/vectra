<div align="center">
  <img src="/img/banner.png" alt="Synentra Banner" />
  <h2>SYNENTRA — Intent-Aware Governance Gateway</h2>

  [![dotnet][dotnet-budge]][dotnet-url]
  [![Build Status][actions-badge]][actions-url]
  [![Quality Gate Status][sonarcloud-quality-gate-badge]][sonarcloud-quality-gate-url]
  [![Reliability Gate Status][sonarcloud-reliability-gate-badge]][sonarcloud-reliability-gate-url]
  [![Maintainability Gate Status][sonarcloud-maintainability-gate-badge]][sonarcloud-maintainability-gate-url]
  [![Security Gate Status][sonarcloud-security-gate-badge]][sonarcloud-security-gate-url]
  [![Vulnerabilities Gate Status][sonarcloud-vulnerabilities-gate-badge]][sonarcloud-vulnerabilities-gate-url]
  [![License: Apache 2.0][apache-badge]][apache-url]
  [![FOSSA License Status][fossa-license-badge]][fossa-license-url]
  [![FOSSA Security Status][fossa-security-badge]][fossa-security-url]
  [![Good First Issues][github-good-first-issue-badge]][github-good-first-issue-url]
</div>

## Table of Contents

- [Overview](#overview-synentra)
  - [Key Capabilities](#key-capabilities)
  - [Why SYNENTRA?](#why-synentra%3F)
- [Key Features](#key-features)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
  - [Run via Docker](#run-via-docker)
  - [Use a Pre-Built Binary](#use-a-pre-built-binary)
  - [Build from Source](#build-from-source)
- [Security](#security)
  - [Reporting a Vulnerability](#reporting-a-vulnerability)
  - [Security Policy](#security-policy)
  - [Dependency Scanning](#dependency-scanning)
- [Community & Contributing](#community-%26-contributing)
  - [Ways to Get Involved](#ways-to-get-involved)
  - [Contributing Code](#contributing-code)
- [License](#license)
- [Support Synentra](#support-synentra)

## Overview: SYNENTRA

> 📖 For full documentation, visit **[synentra.io/docs](https://synentra.io/docs)**.

**SYNENTRA**

While traditional API gateways route traffic based on static endpoints and basic authentication, SYNENTRA introduces a semantic layer of security by evaluating the *actual intent* behind every API call. This allows developers to establish dynamic guardrails, ensuring that AI agents and automated systems operate strictly within defined behavioral boundaries.

> ✨ **Love SYNENTRA? Give it a star!** ✨  
> ⭐ Your support helps others discover the project and fuels continued innovation in AI governance.  
>
> <div align="center" style="padding:2px; background:#dcd4fc; border: 2px solid #eeeeee;"><img src="/img/starring.gif" /></div>

### Key Capabilities

* **Intent-Based Policy Enforcement:** Move beyond standard Role-Based Access Control (RBAC). SYNENTRA analyzes the underlying purpose of a request, allowing you to build context-aware policies that govern *what* an agent is trying to achieve, rather than just *who* the agent is.
* **Human-in-the-Loop (HITL) Safeguards:** Not all automated actions should happen instantly. When SYNENTRA identifies an agent's intent as high-risk, potentially destructive, or malicious, it automatically intercepts the request. The gateway holds the action and routes it to a human operator for manual review and approval before execution.
* **Precise Agent Governance:** As AI agents become more autonomous, the risk of unintended actions grows. SYNENTRA provides the fine-grained control necessary to oversee agent behavior, preventing systemic damage and ensuring compliance.

### Why SYNENTRA?

As organizations deploy more LLM-driven agents and complex microservices, establishing trust in automated workflows is critical. SYNENTRA bridges the gap between automation and safety, providing the necessary oversight to let agents act freely while keeping humans firmly in control of critical decisions.

## Key Features

* ✅**Semantic Intent Analysis:** Evaluates the underlying purpose of every request using natural language understanding, going far beyond simple endpoint matching.
* ✅ **Dynamic Policy Enforcement:** Define and apply context-aware governance rules that adapt to agent behavior and request semantics in real time.
* ✅ **Human-in-the-Loop (HITL):** Automatically intercepts high-risk or ambiguous requests and holds them for manual operator review before execution.
* ✅ **HITL Notifications:** When a request is suspended for review, SYNENTRA can notify reviewers via Slack, Microsoft Teams, PagerDuty, or a generic webhook.
* ✅ **Agent Governance:** Provides fine-grained controls to monitor, restrict, and audit autonomous AI agent actions across your systems.
* ✅ **Agent Quarantine:** Automatically quarantine agents that fall below a trust score threshold, blocking all subsequent requests until manually lifted.
* ✅ **Audit & Observability:** Maintains a full audit trail of agent intent classifications, policy decisions, and HITL review outcomes.
* ✅ **High-Performance Gateway:** Designed for low-latency interception with minimal overhead, keeping your automated workflows fast and responsive.

## Architecture

![SYNENTRA Architecture](/img/architecture.jpg)

Every inbound HTTP request from an AI Agent flows through three layers inside the **SYNENTRA Gateway**:

1. **Request Validation** — checks the API version header, authenticates the caller via JWT, and enforces rate limits. Failures are blocked immediately and recorded in the audit log.
2. **Decision Engine** — valid requests are evaluated by three sequential steps:
   - **Policy Evaluation** — applies configured rules and contextual conditions.
   - **Risk Scoring** — weighs contextual factors including request body, path, anomaly signals, and historical behaviour.
   - **Semantic Analysis** — classifies the underlying intent of the request.
3. **Routing outcome** — based on the decision engine result, the request is one of:
   - ✅ **Direct Allow** → forwarded to the upstream service via the proxy, audit recorded.
   - ⏳ **Pending Review** → held in the **HITL Review** queue for human approval. Approved requests are proxied; disapproved requests are blocked and audited.
   - 🚫 **Policy Block** → blocked immediately, audit recorded.

## Quick Start

### Run via Docker

The fastest way to get SYNENTRA running is with Docker:

```bash
docker pull ghcr.io/synentra/synentra:latest
docker run -p 708:7080 ghcr.io/synentra/synentra:latest
```

SYNENTRA will be available at `http://localhost:7080`.

To supply your own configuration, mount a config file:

```bash
docker run -p 7080:7080 \
  -v $(pwd)/synentra.json:/app/synentra.json \
  ghcr.io/synentra/synentra:latest
```

### Use a Pre-Built Binary

Pre-built binaries for Linux, macOS, and Windows are available on the [Releases](https://github.com/synentra/synentra/releases) page.

1. Download the archive for your platform.
2. Extract and make the binary executable (Linux/macOS):

```bash
tar -xzf synentra-<version>-linux-x64.tar.gz
chmod +x synentra
./synentra
```

3. On Windows, run the extracted executable directly:

```powershell
.\synentra.exe
```

### Build from Source

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

```bash
# Clone the repository
git clone https://github.com/synentra/synentra.git
cd synentra

# Restore dependencies
dotnet restore

# Build the solution
dotnet build --configuration Release

# Run the gateway
dotnet run --project src/Synentra.Gateway --configuration Release
```

To run the full test suite before running:

```bash
dotnet test --configuration Release
```

## Security

Security is a first-class concern in SYNENTRA. We follow responsible disclosure practices and take all reports seriously.

### Reporting a Vulnerability

**Please do not open a public GitHub issue for security vulnerabilities.**

If you discover a security vulnerability, report it privately through one of the following channels:

* **GitHub Private Vulnerability Reporting:** Use the [Report a vulnerability](https://github.com/synentra/synentra/security/advisories/new) button in the **Security** tab of the repository.
* **Email:** Send details to [contact@synentra.io](mailto:contact@synentra.io) with the subject line `[SYNENTRA] Security Vulnerability`.

Please include:
- A description of the vulnerability and its potential impact.
- Steps to reproduce or a proof-of-concept.
- Any relevant environment details (OS, .NET version, Docker image tag, etc.).

We aim to acknowledge reports within **48 hours** and provide a remediation timeline within **7 days**.

### Security Policy

The full security policy, including supported versions and disclosure process, is available in [SECURITY.md](https://github.com/synentra/synentra/blob/main/SECURITY.md).

### Dependency Scanning

SYNENTRA uses [FOSSA](https://fossa.com) for continuous license and security scanning of all dependencies, and [SonarCloud](https://sonarcloud.io) for static analysis. Badge statuses are shown at the top of this file.

## Community & Contributing

SYNENTRA is built in the open and welcomes contributions of all kinds — bug reports, feature requests, documentation improvements, and code.

### Ways to Get Involved

* 🐛 **Report a bug** — [Open an issue](https://github.com/synentra/synentra/issues/new?template=bug_report.md) with steps to reproduce and expected vs. actual behaviour.
* 💡 **Request a feature** — [Open a feature request issue](https://github.com/synentra/synentra/issues/new?template=feature_request.md).
* 🔍 **Pick up a good first issue** — Browse issues labelled [good first issue](https://github.com/synentra/synentra/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22) for beginner-friendly starting points.
* 📖 **Improve the docs** — Spotted something unclear or missing? PRs to docs are always welcome.

### Contributing Code

1. **Fork** the repository and create a feature branch from `main`.
2. **Write tests** for any new behaviour — the project uses `dotnet test`.
3. **Follow** the existing code style and conventions in the codebase.
4. **Open a Pull Request** against `main` with a clear description of what changed and why.

Please read [CONTRIBUTING.md](https://github.com/synentra/synentra/blob/main/CONTRIBUTING.md) for the full contribution guidelines, code of conduct, and PR checklist before submitting.

### Community

Join the Synentra community:

- 💬 Discussions: https://github.com/synentra/synentra/discussions
- 📖 Documentation: https://synentra.io/docs

### Discussion Categories

| Category | Purpose |
|-----------|----------|
| Q&A | Ask questions and get help |
| Ideas | Feature requests and proposals |
| Contributors | Contributor coordination |
| Governance | Project direction and decisions |
| Show & Tell | Share your integrations and projects |
| Announcements | Project updates and releases |

## License

SYNENTRA is open-source and licensed under the **Apache 2.0 License**.  
See [LICENSE](https://github.com/synentra/synentra/blob/main/LICENSE) for details.

## Support SYNENTRA
[![⭐ Star on GitHub](https://img.shields.io/badge/⭐%20Star%20on%20GitHub-555555?style=flat&logo=github)](https://github.com/synentra/synentra)  
✨ **Support SYNENTRA by giving it a star!** ✨  
Your support helps others discover the project and drives continued innovation.

[dotnet-budge]: https://img.shields.io/badge/.NET-10.0-purple
[dotnet-url]: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
[apache-badge]: https://img.shields.io/badge/License-Apache%202.0-blue.svg?style=flat&logo=github
[apache-url]: https://opensource.org/licenses/Apache-2.0
[actions-badge]: https://github.com/synentra/synentra/actions/workflows/build.yml/badge.svg?branch=main
[actions-url]: https://github.com/synentra/synentra/actions?workflow=build
[github-good-first-issue-badge]: https://img.shields.io/github/issues/synentra/synentra/good%20first%20issue?style=flat-square&logo=github&label=good%20first+issues
[github-good-first-issue-url]: https://github.com/synentra/synentra/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22
[sonarcloud-quality-gate-badge]: https://sonarcloud.io/api/project_badges/measure?project=synentra_synentra&metric=alert_status&token=0b5b5ca3c5f12401df0abb73c369c8a620fc174a
[sonarcloud-quality-gate-url]: https://sonarcloud.io/summary/new_code?id=synentra_synentra
[sonarcloud-reliability-gate-badge]: https://sonarcloud.io/api/project_badges/measure?project=synentra_synentra&metric=reliability_rating&token=0b5b5ca3c5f12401df0abb73c369c8a620fc174a
[sonarcloud-reliability-gate-url]: https://sonarcloud.io/summary/new_code?id=synentra_synentra
[sonarcloud-maintainability-gate-badge]: https://sonarcloud.io/api/project_badges/measure?project=synentra_synentra&metric=sqale_rating&token=0b5b5ca3c5f12401df0abb73c369c8a620fc174a
[sonarcloud-maintainability-gate-url]: https://sonarcloud.io/summary/new_code?id=synentra_synentra
[sonarcloud-security-gate-badge]: https://sonarcloud.io/api/project_badges/measure?project=synentra_synentra&metric=security_rating&token=0b5b5ca3c5f12401df0abb73c369c8a620fc174a
[sonarcloud-security-gate-url]: https://sonarcloud.io/summary/new_code?id=synentra_synentra
[sonarcloud-vulnerabilities-gate-badge]: https://sonarcloud.io/api/project_badges/measure?project=synentra_synentra&metric=vulnerabilities&token=0b5b5ca3c5f12401df0abb73c369c8a620fc174a
[sonarcloud-vulnerabilities-gate-url]: https://sonarcloud.io/summary/new_code?id=synentra_synentra
[fossa-license-badge]: https://app.fossa.com/api/projects/git%2Bgithub.com%2Fsynentra%2Fsynentra.svg?type=shield&issueType=license
[fossa-license-url]: https://app.fossa.com/projects/git%2Bgithub.com%2Fsynentra%2Fsynentra?ref=badge_shield&issueType=license
[fossa-security-badge]: https://app.fossa.com/api/projects/git%2Bgithub.com%2Fsynentra%2Fsynentra.svg?type=shield&issueType=security
[fossa-security-url]: https://app.fossa.com/projects/git%2Bgithub.com%2Fsynentra%2Fsynentra?ref=badge_shield&issueType=security