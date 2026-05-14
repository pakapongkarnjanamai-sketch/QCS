# Enterprise API Gateway Platform - Final Implementation Specification

Version: 1.0
Date: 2026-05-09
Status: Final for Development Start
Owner: Enterprise Platform Owner

## 1. Executive Summary

This specification defines an organization-wide gateway platform to centralize access to distributed microservices, standardize API governance, and provide a management UI for service metadata and markdown documentation.

The platform targets the following outcomes:

1. Single entry point for API consumers across QA and PR for all business systems.
2. Central catalog for categories, services, endpoints, and environment routes.
3. Git-first markdown documentation per endpoint for both humans and AI agents.
4. Strict QA to PR promotion flow using the same approved artifact.

This document is complete for MVP implementation kickoff in 2 to 4 weeks.

## 2. Product Requirements Document (PRD)

### 2.1 Problem Statement

Organization data and APIs are distributed across multiple microservices and hosts across business domains. Teams spend significant effort finding endpoints, validating environment URLs, and troubleshooting integration drift.

Current pain points:

1. API metadata and environment routes are spread across multiple files and services.
2. Documentation coverage is inconsistent and hard to keep current.
3. Integration and deployment errors occur when QA and PR paths differ.
4. AI agents cannot reliably discover complete, structured service context.

### 2.2 Product Vision

Build an internal Gateway Admin Platform that provides:

1. Runtime API routing.
2. Metadata management UI.
3. Markdown documentation management.
4. Machine-readable catalog APIs for AI agents.

### 2.3 Goals

MVP goals:

1. Route API traffic through one gateway domain with environment-aware and system-aware paths.
2. Manage categories, services, endpoints, and docs via web UI.
3. Import or refresh endpoint definitions from upstream Swagger.
4. Enforce QA-first release workflow and controlled PR promotion.

### 2.4 Non-Goals

MVP excludes:

1. Full enterprise service mesh.
2. API monetization or billing.
3. External developer portal for public clients.
4. Advanced graph analytics and dependency mapping.

### 2.5 Stakeholders

1. Product owner and release approver: single decision maker.
2. Internal developers consuming APIs.
3. Operations and support teams.
4. Domain system owners for each onboarded service group.
5. AI agents consuming catalog and docs.

### 2.6 User Roles

1. Platform Admin: full CRUD and release promotion rights.
2. Editor: CRUD on metadata and docs, no PR promotion.
3. Viewer: read-only access.

### 2.7 Core Use Cases

1. Add a new service under a specific system namespace with QA and PR routes.
2. Group services into clear categories.
3. Add, edit, and retire endpoint definitions.
4. Add markdown docs per endpoint and publish to Git.
5. Import endpoint list from upstream OpenAPI.
6. Run QA validation checklist and promote to PR.
7. Allow AI agents to query structured catalog and docs index.

### 2.8 Functional Requirements

1. Category CRUD.
2. System namespace management as part of service configuration.
3. Service CRUD.
4. Service environment route CRUD for QA and PR.
5. Endpoint CRUD with policy configuration.
6. Markdown doc create, edit, preview, version history.
7. OpenAPI import and endpoint sync.
8. Release runbook tracking and QA to PR promotion record.
9. Catalog search and export APIs for AI agents.

### 2.9 Non-Functional Requirements

1. Availability target for gateway runtime: 99.5 percent.
2. P95 route overhead at gateway: under 100 ms.
3. Full audit trail on metadata and docs mutations.
4. Correlation ID propagation across all proxied requests.
5. Role-based authorization for admin APIs and UI.

### 2.10 Success Metrics

1. 100 percent of exposed endpoints have markdown docs.
2. 100 percent of PR releases have linked QA pass evidence.
3. 50 percent reduction in time to locate correct endpoint and environment route.
4. Zero direct PR deployments without prior QA validation.
5. At least 3 business systems onboarded through one gateway governance model in phase 1.

## 3. Architecture

### 3.1 Technology Stack

1. Backend: ASP.NET Core .NET 9.
2. Gateway runtime: YARP on ASP.NET Core.
3. Frontend admin: React 19 plus TypeScript.
4. Database: SQL Server 2022.
5. Docs storage: Git-first markdown files.

### 3.2 Proposed Solution Structure

1. Enterprise.Gateway.API
2. Enterprise.Gateway.Application
3. Enterprise.Gateway.Domain
4. Enterprise.Gateway.Infrastructure
5. Enterprise.Gateway.Admin
6. enterprise-gateway-docs

### 3.3 Logical Components

1. Gateway Runtime: routes, transforms, rate limits, retries, timeout.
2. Catalog API: metadata and release APIs.
3. Docs API: markdown CRUD, preview, git publish workflow.
4. Swagger Sync Worker: fetch and map OpenAPI endpoints.
5. Admin UI: operator-facing management portal.
6. Observability: logs, traces, correlation IDs, health checks.

### 3.4 Environment Topology and Rollout Waves

The platform is organization-wide. The server map below is the current rollout
wave for existing QCS-aligned infrastructure and should be treated as wave 1,
not the total platform boundary.

PR series:

1. AP-NTC2139-COSS, 10.10.154.119, SQL Server 2022.
2. AP-NTC2137-PRWB, 10.10.154.21, IIS.
3. AP-NTC2138-PRAP, 10.10.154.136, IIS.

QA series:

1. AP-NTC2138-QAAP, 10.10.143.38, IIS.
2. AP-NTC2138-QAWB, 10.10.143.39, IIS.
3. AP-NTC2138-QADB, 10.10.143.37, SQL Server 2022.

### 3.5 Security Model

1. Admin UI and admin APIs use Windows Authentication and role-based authorization.
2. Runtime gateway can support pass-through auth headers or service identity mode.
3. Sensitive settings stored via environment-specific secured configuration.
4. All admin mutations are audited with actor and timestamp.

### 3.6 Runtime Route Model

Gateway URL pattern:

1. /gateway/{environment}/{systemKey}/{serviceKey}/{*path}

Examples:

1. /gateway/qa/qcs/vendor/api/Vendors/LookupVendors
2. /gateway/pr/qcs/employee/api/EmployeeLookup/GetFull
3. /gateway/qa/hr/payroll/api/Payroll/Employees

Rules:

1. `systemKey` identifies the owning business system or domain.
2. `serviceKey` identifies the service within that system namespace.
3. The pair (`systemKey`, `serviceKey`) must be unique.

### 3.7 AI Agent Integration Model

Expose machine-readable catalog endpoints:

1. /catalog/export/services
2. /catalog/export/endpoints
3. /catalog/export/docs-index
4. /catalog/search

Each endpoint record includes:

1. category.
2. system key.
3. service key.
4. method.
5. path.
6. auth mode.
7. qa route.
8. pr route.
9. doc path.
10. last reviewed timestamp.

## 4. API Specification

Base URL for admin APIs:

1. /admin

Base URL for runtime gateway APIs:

1. /gateway/{environment}/{systemKey}/{serviceKey}/...

### 4.1 Category APIs

1. GET /admin/catalog/categories
2. POST /admin/catalog/categories
3. PUT /admin/catalog/categories/{id}
4. DELETE /admin/catalog/categories/{id}

Category payload:

```json
{
  "code": "employee",
  "name": "Employee Services",
  "description": "Directory and employee-related services",
  "isActive": true,
  "sortOrder": 10
}
```

### 4.2 Service APIs

1. GET /admin/catalog/services
2. POST /admin/catalog/services
3. PUT /admin/catalog/services/{id}
4. DELETE /admin/catalog/services/{id}
5. POST /admin/catalog/services/{id}/sync-openapi

Service payload:

```json
{
  "categoryId": 1,
  "systemKey": "qcs",
  "serviceKey": "employee",
  "serviceName": "Employee.Service",
  "owner": "Platform-Team",
  "authMode": "WindowsAuth",
  "isActive": true
}
```

System namespace behavior:

1. `systemKey` is required for every service.
2. `serviceKey` uniqueness is enforced inside each `systemKey` namespace.
3. `systemKey` and `serviceKey` should follow lowercase slug format.

### 4.3 Environment Route APIs

1. GET /admin/catalog/services/{id}/environments
2. POST /admin/catalog/services/{id}/environments
3. PUT /admin/catalog/service-environments/{envId}
4. DELETE /admin/catalog/service-environments/{envId}

Environment route payload:

```json
{
  "environment": "QA",
  "baseUrl": "https://ap-ntc2138-qawb/iChaSue/Service/Vendor/",
  "healthUrl": "https://ap-ntc2138-qawb/iChaSue/Service/Vendor/swagger/index.html",
  "timeoutMs": 30000,
  "isActive": true
}
```

### 4.4 Endpoint APIs

1. GET /admin/catalog/endpoints
2. POST /admin/catalog/endpoints
3. PUT /admin/catalog/endpoints/{id}
4. DELETE /admin/catalog/endpoints/{id}
5. POST /admin/catalog/endpoints/{id}/toggle

Endpoint payload:

```json
{
  "serviceId": 3,
  "method": "GET",
  "path": "/api/EmployeeLookup/GetFull",
  "upstreamPath": "/api/EmployeeLookup/GetFull",
  "summary": "Employee full profile lookup",
  "authMode": "WindowsAuth",
  "isExposed": true,
  "sortOrder": 120
}
```

### 4.5 Endpoint Policy APIs

1. GET /admin/catalog/endpoints/{id}/policy
2. PUT /admin/catalog/endpoints/{id}/policy

Policy payload:

```json
{
  "timeoutMs": 30000,
  "retryCount": 2,
  "retryBackoffMs": 500,
  "circuitBreakerEnabled": true,
  "rateLimitPerMinute": 120,
  "cacheSeconds": 0
}
```

### 4.6 Markdown Docs APIs

1. GET /admin/catalog/docs/{endpointId}
2. PUT /admin/catalog/docs/{endpointId}
3. GET /admin/catalog/docs/{endpointId}/history
4. POST /admin/catalog/docs/{endpointId}/publish

Doc update payload:

```json
{
  "title": "GET EmployeeLookup GetFull",
  "markdown": "---\nservice: Employee.Service\nmethod: GET\npath: /api/EmployeeLookup/GetFull\n---\n...",
  "changeNote": "Update sample response fields"
}
```

### 4.7 Release APIs

1. GET /admin/releases
2. POST /admin/releases
3. POST /admin/releases/{id}/qa-validate
4. POST /admin/releases/{id}/promote-pr
5. POST /admin/releases/{id}/rollback

Release create payload:

```json
{
  "releaseName": "2026.05.09-gateway-mvp",
  "artifactVersion": "1.0.0+20260509.1",
  "scope": "gateway-config-and-catalog"
}
```

### 4.8 Agent Export APIs

1. GET /admin/catalog/export/services
2. GET /admin/catalog/export/endpoints
3. GET /admin/catalog/export/docs-index
4. GET /admin/catalog/search?q=employee lookup

### 4.9 Standard Error Format

All APIs return RFC 7807 style problem details on failure:

```json
{
  "type": "https://gateway/errors/validation",
  "title": "Validation error",
  "status": 400,
  "detail": "Service key already exists",
  "instance": "/admin/catalog/services",
  "correlationId": "7c0f12f9-7cc3-4bbf-9e52-1b0b4a5d2f91"
}
```

## 5. Database Design

### 5.1 Core Tables

1. categories
2. services
3. service_environments
4. api_endpoints
5. endpoint_policies
6. api_docs
7. api_doc_versions
8. releases
9. release_items
10. audit_logs
11. swagger_sync_jobs

### 5.2 Minimal Schema Definition

```sql
CREATE TABLE categories (
  id INT IDENTITY PRIMARY KEY,
  code NVARCHAR(50) NOT NULL UNIQUE,
  name NVARCHAR(200) NOT NULL,
  description NVARCHAR(1000) NULL,
  is_active BIT NOT NULL DEFAULT 1,
  sort_order INT NOT NULL DEFAULT 100,
  created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at DATETIME2 NULL,
  created_by NVARCHAR(100) NULL,
  updated_by NVARCHAR(100) NULL
);

CREATE TABLE services (
  id INT IDENTITY PRIMARY KEY,
  category_id INT NOT NULL,
  system_key NVARCHAR(50) NOT NULL,
  service_key NVARCHAR(50) NOT NULL,
  service_name NVARCHAR(200) NOT NULL,
  owner NVARCHAR(100) NULL,
  auth_mode NVARCHAR(50) NOT NULL,
  is_active BIT NOT NULL DEFAULT 1,
  created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at DATETIME2 NULL,
  created_by NVARCHAR(100) NULL,
  updated_by NVARCHAR(100) NULL,
  CONSTRAINT uq_service_namespace UNIQUE (system_key, service_key),
  CONSTRAINT fk_services_categories FOREIGN KEY (category_id) REFERENCES categories(id)
);

CREATE TABLE service_environments (
  id INT IDENTITY PRIMARY KEY,
  service_id INT NOT NULL,
  environment NVARCHAR(20) NOT NULL,
  base_url NVARCHAR(1000) NOT NULL,
  health_url NVARCHAR(1000) NULL,
  timeout_ms INT NOT NULL DEFAULT 30000,
  is_active BIT NOT NULL DEFAULT 1,
  created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at DATETIME2 NULL,
  CONSTRAINT uq_service_env UNIQUE (service_id, environment),
  CONSTRAINT fk_service_env_services FOREIGN KEY (service_id) REFERENCES services(id)
);

CREATE TABLE api_endpoints (
  id INT IDENTITY PRIMARY KEY,
  service_id INT NOT NULL,
  method NVARCHAR(10) NOT NULL,
  path NVARCHAR(1000) NOT NULL,
  upstream_path NVARCHAR(1000) NOT NULL,
  summary NVARCHAR(500) NULL,
  auth_mode NVARCHAR(50) NOT NULL,
  is_exposed BIT NOT NULL DEFAULT 1,
  sort_order INT NOT NULL DEFAULT 100,
  created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_at DATETIME2 NULL,
  CONSTRAINT uq_endpoint UNIQUE (service_id, method, path),
  CONSTRAINT fk_endpoints_services FOREIGN KEY (service_id) REFERENCES services(id)
);

CREATE TABLE endpoint_policies (
  endpoint_id INT PRIMARY KEY,
  timeout_ms INT NOT NULL DEFAULT 30000,
  retry_count INT NOT NULL DEFAULT 1,
  retry_backoff_ms INT NOT NULL DEFAULT 500,
  circuit_breaker_enabled BIT NOT NULL DEFAULT 0,
  rate_limit_per_minute INT NOT NULL DEFAULT 120,
  cache_seconds INT NOT NULL DEFAULT 0,
  updated_at DATETIME2 NULL,
  CONSTRAINT fk_policy_endpoint FOREIGN KEY (endpoint_id) REFERENCES api_endpoints(id)
);

CREATE TABLE api_docs (
  endpoint_id INT PRIMARY KEY,
  title NVARCHAR(300) NOT NULL,
  doc_slug NVARCHAR(300) NOT NULL UNIQUE,
  doc_path NVARCHAR(1000) NOT NULL,
  markdown_current NVARCHAR(MAX) NOT NULL,
  last_reviewed_at DATETIME2 NULL,
  updated_at DATETIME2 NULL,
  updated_by NVARCHAR(100) NULL,
  CONSTRAINT fk_docs_endpoint FOREIGN KEY (endpoint_id) REFERENCES api_endpoints(id)
);

CREATE TABLE api_doc_versions (
  id BIGINT IDENTITY PRIMARY KEY,
  endpoint_id INT NOT NULL,
  version_no INT NOT NULL,
  markdown_content NVARCHAR(MAX) NOT NULL,
  change_note NVARCHAR(1000) NULL,
  committed_sha NVARCHAR(100) NULL,
  created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by NVARCHAR(100) NULL,
  CONSTRAINT fk_doc_versions_endpoint FOREIGN KEY (endpoint_id) REFERENCES api_endpoints(id)
);

CREATE TABLE releases (
  id BIGINT IDENTITY PRIMARY KEY,
  release_name NVARCHAR(200) NOT NULL,
  artifact_version NVARCHAR(100) NOT NULL,
  status NVARCHAR(50) NOT NULL,
  qa_validated_at DATETIME2 NULL,
  pr_promoted_at DATETIME2 NULL,
  created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by NVARCHAR(100) NULL,
  approved_by NVARCHAR(100) NULL
);

CREATE TABLE release_items (
  id BIGINT IDENTITY PRIMARY KEY,
  release_id BIGINT NOT NULL,
  item_type NVARCHAR(50) NOT NULL,
  item_id NVARCHAR(100) NOT NULL,
  before_hash NVARCHAR(128) NULL,
  after_hash NVARCHAR(128) NULL,
  CONSTRAINT fk_release_items_release FOREIGN KEY (release_id) REFERENCES releases(id)
);

CREATE TABLE audit_logs (
  id BIGINT IDENTITY PRIMARY KEY,
  entity_type NVARCHAR(100) NOT NULL,
  entity_id NVARCHAR(100) NOT NULL,
  action NVARCHAR(30) NOT NULL,
  before_json NVARCHAR(MAX) NULL,
  after_json NVARCHAR(MAX) NULL,
  actor NVARCHAR(100) NULL,
  created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  correlation_id NVARCHAR(100) NULL
);
```

### 5.3 Required Indexes

1. services(system_key, service_key) unique.
2. api_endpoints(service_id, method, path) unique.
3. service_environments(service_id, environment) unique.
4. api_doc_versions(endpoint_id, version_no) unique.
5. audit_logs(created_at) nonclustered.
6. audit_logs(correlation_id) nonclustered.

## 6. UI Specification

### 6.1 Screen Map

1. Dashboard.
2. Categories.
3. Services.
4. Endpoints.
5. Docs.
6. Release Center.
7. Audit Logs.

### 6.2 Dashboard

Required widgets:

1. Total categories.
2. Total active services.
3. Total active endpoints.
4. Documentation coverage percent.
5. Last QA validation result.
6. Last PR promotion summary.

### 6.3 Categories Screen

Capabilities:

1. Create category.
2. Edit category.
3. Disable category.
4. Delete category only when no services reference it.

Validation:

1. code required, unique, max 50 chars.
2. name required, max 200 chars.

### 6.4 Services Screen

Capabilities:

1. Create service with category assignment.
2. Manage QA and PR environment routes.
3. Trigger Swagger sync.
4. View health and last sync timestamp.

Validation:

1. serviceKey required, unique within systemKey, lowercase slug pattern.
2. authMode required.
3. QA and PR base URLs required for active services.

### 6.5 Endpoints Screen

Capabilities:

1. Add endpoint manually.
2. Edit endpoint fields.
3. Toggle exposure state.
4. Configure policy.
5. Link to docs editor.

Validation:

1. method required.
2. path required and starts with slash.
3. duplicate method plus path blocked per service.

### 6.6 Docs Screen

Capabilities:

1. Edit markdown with frontmatter template.
2. Preview markdown.
3. View version history.
4. Publish to Git.

Required frontmatter template:

```yaml
---
service: Employee.Service
system: qcs
category: EmployeeLookup
method: GET
path: /api/EmployeeLookup/GetFull
auth: WindowsAuth
qaUrl: https://ap-ntc2138-qawb/... 
prUrl: https://ap-ntc2137-prwb/... 
owner: Platform-Team
lastReviewed: 2026-05-09
---
```

### 6.7 Release Center Screen

Capabilities:

1. Create release record with artifact version.
2. Run QA checklist and attach evidence.
3. Promote to PR only when QA is marked passed.
4. Trigger rollback workflow.

Blocking rule:

1. Promote button disabled until QA validation is completed and approved.

## 7. Release Playbook

### 7.1 Release Policy

Mandatory order:

1. Deploy QA first.
2. Validate QA.
3. Promote same artifact to PR.

No direct PR deployment without QA, except explicit emergency approval.

### 7.2 Standard Release Steps

1. Build once and assign artifact version.
2. Deploy to QA.
3. Run smoke tests and API verification.
4. Record QA evidence and approve.
5. Promote same artifact to PR.
6. Run PR smoke tests.
7. Mark release complete.

### 7.3 QA Smoke Checklist

1. Gateway health endpoints return success.
2. At least one endpoint per onboarded system category is reachable.
3. Authentication and authorization checks pass.
4. Docs API returns valid markdown metadata for changed endpoints.
5. Logging and correlation IDs appear in log store.

### 7.4 PR Smoke Checklist

1. Same checks as QA executed in PR.
2. Route base URLs match PR environment map.
3. No QA-only URLs remain in active PR routes.

### 7.5 Rollback Strategy

1. Config rollback to previous release snapshot.
2. Artifact rollback to previous stable version.
3. Database rollback only for non-backward-compatible migrations, with approved script.

### 7.6 Emergency Hotfix

1. Emergency PR deploy allowed only with explicit owner approval.
2. Post-deploy backfill required: reproduce release record and QA verification as soon as possible.

## 8. Implementation Plan (2 to 4 Weeks)

### Week 1

1. Solution scaffolding and base architecture.
2. Catalog DB schema and migrations.
3. Category and service CRUD APIs.
4. Basic admin UI shell and authentication.

### Week 2

1. Endpoint and policy APIs.
2. Gateway runtime route resolution.
3. Environment route management UI.
4. Swagger sync MVP.

### Week 3

1. Markdown docs API and UI editor with preview.
2. Git publish workflow and doc versioning.
3. Agent export APIs and search.
4. Audit log implementation.

### Week 4

1. Release center and QA to PR promotion flow.
2. Full smoke test automation.
3. Hardening, performance checks, operational runbook.
4. Production readiness review.

## 9. Risks and Mitigations

1. Risk: endpoint drift after upstream changes.
2. Mitigation: scheduled swagger sync and diff review.

3. Risk: missing docs for new endpoints.
4. Mitigation: release gate requiring doc coverage for changed endpoints.

5. Risk: inconsistent environment URLs.
6. Mitigation: environment route validation and pre-release checks.

7. Risk: AI reads stale metadata.
8. Mitigation: export endpoints include lastUpdated and release version.

## 10. Final Definition of Done

1. Gateway routes active for QA and PR with environment-scoped URLs.
2. Gateway supports system-scoped routing (`systemKey` plus `serviceKey`).
3. Admin UI supports full CRUD for categories, services, endpoints, and docs.
4. Markdown docs are Git-first with version history and publish flow.
5. Catalog export APIs available for AI agent usage.
6. QA to PR promotion workflow enforced and auditable.
7. Correlation ID and audit logs enabled for all critical actions.
8. Runbook and smoke tests executed successfully in QA and PR.
9. At least one non-QCS system onboarded in addition to QCS pilot services.

## 11. Initial Backlog for Development Kickoff

1. Create solution and projects for API, application, domain, infrastructure, and admin UI.
2. Implement authentication and authorization middleware.
3. Implement category and service modules end to end, including system namespace support.
4. Implement endpoint and policy modules end to end.
5. Implement docs module with markdown template and Git publish.
6. Implement release module with QA gate and PR promotion action.
7. Implement observability and audit baseline.
8. Prepare deployment scripts for QA first and PR promotion.
9. Onboard initial systems: QCS plus at least one additional organization system.
