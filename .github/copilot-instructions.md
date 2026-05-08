# QCS Copilot Instructions

## Azure
- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.

---

## Project Overview
QCS (Quotation Compare System) is a procurement and quotation management platform.
- **Backend**: ASP.NET Core Web API (`QCS.API`) — Clean Architecture (Domain / Application / Infrastructure / API)
- **Frontend**: React 19 + TypeScript + Tailwind CSS (`QCS.React.Admin`) — Vite 8, DevExtreme React 24.2
- **Admin portal (legacy)**: ASP.NET Core MVC (`QCS.Web.Admin`)
- **User portal (legacy)**: ASP.NET Core MVC (`QCS.Web.User`)

### Business domain summary
- QCS is used to create and control **Quotation** documents under company procurement rules.
- Every quotation flow must include **quotation comparison** before final approval.
- A request must pass approvals from multiple departments through a defined **Workflow**.
- Quotation source files are uploaded as PDF documents (multiple document types).
- Final output is a complete approved quotation package that can be used as an official document.

### Quotation document lifecycle
1. Create request and upload quotation PDFs.
2. Compare quotations according to business rules.
3. Route approvals by workflow steps and approvers.
4. After all approvals are completed, call PDF service to merge and stamp files.
5. Return a finalized PDF package for downstream usage.

### PDF merge/stamp integration
- Finalized PDF generation is handled by `IQuotationService` in `QCS.Application`.
- Main method for approved documents: `GenerateStampedPdfAsync`.
- Behavior:
  - Collect PDF attachments from approved request data.
  - Build approval stamp payload from workflow approval steps.
  - Send payload to external PDF API (`/api/Pdf/merge-stamp`) for merge and stamping.
  - Return a completed PDF file (official output) to API consumers.

---

## Backend (.NET) Conventions

### Architecture
- **Domain** (`QCS.Domain`): models, DTOs, enums — no dependencies on other layers.
- **Application** (`QCS.Application`): service interfaces and implementations, hubs. Depends only on Domain.
- **Infrastructure** (`QCS.Infrastructure`): EF Core DbContext, migrations, external service implementations.
- **API** (`QCS.API`): controllers, middleware, DI wiring. No business logic here.

### DataGrid API endpoints
- Use `DevExtreme.AspNet.Data.DataSourceLoader.Load(query, loadOptions)` in controllers.
- Return the raw result object — do not wrap in a custom envelope.
- Endpoint pattern: `GET /api/<Controller>/<GridName>` accepting `DataSourceLoadOptions` as query params.
- Mark grid endpoints with `[HttpGet]` only; no `[FromBody]`.

### General API
- All controllers use `[Authorize]` unless explicitly public.
- Return `IActionResult` for mutation endpoints; use `Ok(new { success = true })` shape.
- Use `Problem(...)` for 400-level errors, `NotFound(message)` for 404.
- DTOs live in `QCS.Domain/DTOs/`. Prefix with context noun (e.g. `RequestGridDto`, `CreateRequestDto`).

---

## Frontend (React) Conventions

### Stack
- **Vite 8** + **React 19** + **TypeScript** (strict)
- **Tailwind CSS 4** via `@tailwindcss/vite` plugin (no `tailwind.config.js`)
- **react-router-dom 7** with `BrowserRouter` and `basename` from `appConfig`
- **DevExtreme 24.2** (`devextreme` + `devextreme-react`)

### Project structure
```
src/
  config/
    appConfig.ts         # env-based config: appBasePath, apiBaseUrl, hubUrl
    navigation.ts        # NAV_GROUPS, PAGE_TITLES, getPageTitle
  components/
    layout/              # AppLayout, Sidebar (shell, not page-specific)
    ui/                  # Reusable: Toolbar, TableSurface, SidePanel
  lib/
    createDataSource.ts  # CustomStore factory for DX DataGrid remote operations
  pages/
    <feature>/           # One folder per real feature page (e.g. requests/RequestsPage.tsx)
    pageData.ts          # Placeholder WorkspaceDefinition data for shell-only pages
  devextreme-license.ts
  main.tsx
  App.tsx
```

### App config
- All environment variables must be prefixed `VITE_QCS_`.
- Access config only via `import { appConfig } from '../config/appConfig.ts'` — never `import.meta.env` directly in feature code.
- Current keys: `appBasePath`, `apiBaseUrl`, `hubUrl`.

### DevExtreme usage
- Register the license once in `main.tsx` via `config({ licenseKey })` from `devextreme/core/config`.
- Always import DX CSS in `main.tsx`: `import 'devextreme/dist/css/dx.light.css'`.
- For DataGrid remote data: use `createDataSource(path, key)` from `src/lib/createDataSource.ts`.
  - This builds a `CustomStore` that sends all `LoadOptions` fields to the server as query params.
  - Enable `<RemoteOperations filtering paging sorting />` in the DataGrid.
- When looking up DevExtreme API: search `mcp_dxdocs24_2_devexpress_docs_search` first (technology: `React`, version 24.2).
- Do NOT import `devextreme/dist/css/dx.light.css` in individual page components — it is already global.

### Routing
- Shell pages (placeholder/mockup) are registered via `workspacePages` array in `pageData.ts` and rendered by `WorkspacePage`.
- Real feature pages get their own component under `src/pages/<feature>/` and are wired explicitly in `App.tsx`.
- When a feature page is implemented for real, **remove its entry from `workspacePages`** and add a dedicated `<Route>` in `App.tsx`.

### Styling
- Use design tokens defined in `src/index.css` (OKLCH-based CSS custom properties): `--ink-strong`, `--ink-muted`, `--ink-soft`, `--surface-panel`, `--surface-muted`, `--border-subtle`.
- 8px grid, 13px base typography, sharp low-radius geometry (`rounded-sm`), no gradients, no shadows.
- Tailwind utility classes only — no inline `style` except for `fontFamily` / `fontFeatureSettings`.
- Do NOT add badges, chips, counters, hero sections, or decorative icons.

### Reusable UI components (`src/components/ui/`)
- **`Toolbar`**: search input + filter button group. Props: `title`, `description?`, `searchPlaceholder?`, `filters[]`, `activeFilterIndex`, `onSearch`, `onFilterChange`.
- **`TableSurface<TRow>`**: generic typed table. Props: `columns: TableColumn<TRow>[]`, `rows`, `rowKey`, `actionLabel?`, `onAction?`.
- **`SidePanel`**: definition list panel. Props: `title`, `items: { label, value: ReactNode }[]`.
- Prefer these components for non-DX tables. Use DX `DataGrid` for API-backed grids with remote operations.

### Code style
- TypeScript strict mode; no `any` unless wrapping untyped third-party API.
- Named exports only — no default exports for components.
- One component per file; file name matches exported function name.
- No `useEffect` for derived state — compute directly or use `useMemo`.
- Do not add JSDoc, comments, or type annotations to code you did not change.

---

## Design Context

### Users
- Primary scope for frontend work: `QCS.React.Admin`.
- Primary users: IT/System Admin.
- Secondary users: procurement admin staff.
- Working context: office/daytime operations with direct, grid-first navigation.

### Brand Personality
- Friendly, clever, clean.
- Emotional goal: confidence and trust through predictable, unambiguous UI behavior.

### Aesthetic Direction
- Corporate-refined, data-dense.
- Light mode only, English-first UI copy for React Admin.
- DevExtreme Fluent Blue Light is fixed; blue primary remains anchor.
- Reference feel: GitHub clarity/density and Google AI Studio clean framing.
- Avoid overusing color, inconsistent button styles, decorative effects, and generic SaaS metric-hero layouts.

### Design Principles
1. Confidence through clarity.
2. Data-first density with clear scanability.
3. Consistent action language (button styles and states).
4. Restrained color usage with blue as anchor.
5. Quiet, purposeful interaction and motion.
6. Admin precision.

### Accessibility & Motion Baseline
- Accessibility target: WCAG 2.1 AA baseline.
- Motion policy: keep current motion behavior by default (no automatic reduced-motion override unless explicitly requested per feature).
