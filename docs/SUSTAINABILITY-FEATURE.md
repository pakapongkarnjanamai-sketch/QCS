# Sustainability Feature - Paper Saved Tracking

## Overview

The **Sustainability Feature** tracks the environmental impact reduction from QCS's digital quotation workflow. By measuring PDF pages in approved quotations instead of printed sheets, the system quantifies CO₂, water, and tree-equivalent savings.

**Goal:** Enable procurement teams to measure and communicate the environmental benefit of the quotation digitalization initiative.

---

## Business Value

- **Environmental Impact Visibility:** Dashboard shows tangible metrics (sheets saved, CO₂ avoided, water saved, tree equivalents).
- **Compliance & ESG Reporting:** Support corporate sustainability goals and ESG disclosures.
- **Stakeholder Communication:** Demonstrate cost and environmental benefits of the digital platform.
- **Behavioral Incentive:** Publicize metrics to reinforce the value of using QCS over paper-based workflows.

---

## How It Works

### 1. Page Counting

**For new uploads (going forward):**
- When a user uploads a quotation PDF, `FileService` calls `IPdfPageCounter` to extract page count
- The `PageCount` is stored in `AttachmentFile.PageCount` during upload
- Uses **PdfPig** library (MIT-licensed) to avoid licensing overhead in the API process

**For legacy attachments (before this feature):**
- Run the **Backfill** endpoint to count pages from existing PDFs
- Marks files as `PageCount = 0` if extraction fails (prevents infinite retries)
- Results visible on the Sustainability dashboard after backfill completes

### 2. Summary Calculation

`GetSummaryAsync()` fetches:
```
WHERE Request.Status = Approved 
  AND Quotation.AttachmentFile.PageCount IS NOT NULL
```

Results:
- **Total Pages** — sum of all `PageCount` values
- **Quotation Files** — count of files with PageCount
- **Approved Requests** — total approved requests (denominator for comparison)

Then calculates environmental impact:
```
CO₂ Saved = TotalPages × 4.6 g/sheet
Water Saved = TotalPages × 10 L/sheet
Trees Equivalent = TotalPages ÷ 8,333 sheets/tree
```

### 3. Trend Analysis

`GetTrendAsync(timeframe, aggregation)` groups approved quotations by:
- **Timeframe:** `7d` | `30d` | `6m` | `1y`
- **Aggregation:** `day` | `week` | `month`

Uses shared `TrendBuckets` helper (same logic as Request Trend) to build date ranges and sum pages per bucket.

### 4. Legacy Data Backfill

`BackfillPageCountsAsync(batchSize)`:
1. Selects up to `batchSize` (default 50, max 500) attachments where `PageCount IS NULL AND Data IS NOT NULL`
2. Attempts to count pages for each using `PdfPageCounter`
3. On success: sets `PageCount = actual page count`
4. On failure: sets `PageCount = 0` (prevents retry loops)
5. Returns: `{ Processed, Updated, Failed, Remaining }` count

Call repeatedly until `Remaining = 0` to backfill all legacy PDFs.

---

## Architecture

### Backend (ASP.NET Core)

**Domain Layer** (`QCS.Domain`)
- `AttachmentFile.PageCount` — nullable int, stores PDF page count or 0 if uncountable

**Application Layer** (`QCS.Application`)
- `IPdfPageCounter` — abstraction for page extraction
- `IPaperSavedService` — business logic for summaries, trends, backfill
- `TrendBuckets` — shared time-bucketing helper

**Infrastructure Layer** (`QCS.Infrastructure`)
- `PdfPigPageCounter` — implementation using UglyToad.PdfPig (MIT)
- Validates `ContentType` contains "pdf" before parsing
- Returns `null` on parse failures (logged as warning)

**API Layer** (`QCS.API`)
- `DashboardController` (updated)
  - `GET /api/Dashboard/PaperSaved` → `PaperSavedDto`
  - `GET /api/Dashboard/PaperSavedTrend?timeframe=&aggregation=` → `List<PaperSavedTrendPointDto>`
  - `POST /api/Dashboard/BackfillPageCount?batchSize=` → `PaperSavedBackfillResultDto`

### Frontend (React)

**Pages**
- **OverviewPage** — New card showing Sheets saved + secondary metrics (CO₂, water, trees) with link to dedicated page
- **SustainabilityPage** — Full dashboard with hero summary, KPIs, and trend chart with timeframe/aggregation controls

**Navigation**
- New "Sustainability" nav item in Sidebar, routed to `/QCS/admin/sustainability`
- Uses leaf-style SVG icon to denote environmental theme

**Data Flow**
1. `SustainabilityPage` fetches `/api/Dashboard/PaperSaved` and `/api/Dashboard/PaperSavedTrend`
2. Displays 4 KPI cards with formatted numbers
3. Trend chart (bar) shows sheets saved per time bucket
4. "Count older PDFs" button calls POST `/api/Dashboard/BackfillPageCount` and refreshes summary

---

## Constants & Assumptions

| Metric | Value | Source |
|--------|-------|--------|
| CO₂ per A4 sheet | 4.6 g | Industry standard (paper production) |
| Water per A4 sheet | 10 L | Industry standard (paper production) |
| A4 sheets per tree | 8,333 | Industry approximation |
| Batch size (backfill) | 50 (default), max 500 | Performance tuning |

These are conservative estimates. Update constants in `PaperSavedService.cs` if your organization has different ESG targets.

---

## Deployment & Activation

### Database

```bash
dotnet ef database update --project QCS.Infrastructure --startup-project QCS.API
```

Applies migration `20260520044803_AddPageCountToAttachmentFile` to add nullable `PageCount` column to `AttachmentFiles` table.

### Backend Deploy

Run existing deploy script:
```bash
.\QCS.API\scripts\Deploy-QCS-API.ps1
```

Publishes API, copies to IIS target, and runs smoke tests including new `/api/Dashboard/PaperSaved` endpoint.

### Frontend Deploy

Run existing deploy script:
```bash
.\QCS.React.Admin\scripts\Deploy-QCS-React-Admin.ps1
```

Builds React app with production environment variables, copies dist to IIS, and validates SPA deep-linking.

### Post-Deploy Activation

1. Navigate to `https://<your-domain>/QCS/admin/sustainability`
2. Click **"Count older PDFs"** to backfill legacy attachments
3. Repeat until `Remaining: 0`
4. Dashboard metrics will populate automatically
5. New uploads from this point forward will have page counts recorded immediately

---

## Implementation Details

### Files Created

| Path | Purpose |
|------|---------|
| `QCS.Domain/DTOs/PaperSavedDto.cs` | DTOs for summary, trend points, backfill result |
| `QCS.Application/Abstractions/IPdfPageCounter.cs` | Service abstraction for PDF page extraction |
| `QCS.Infrastructure/Services/PdfPigPageCounter.cs` | Concrete implementation using PdfPig library |
| `QCS.Application/Services/PaperSavedService.cs` | Core business logic: summary, trend, backfill |
| `QCS.Application/Services/TrendBuckets.cs` | Shared time-bucketing helper for daily/weekly/monthly |
| `QCS.React.Admin/src/pages/sustainability/SustainabilityPage.tsx` | Dedicated sustainability dashboard |

### Files Modified

| Path | Changes |
|------|---------|
| `QCS.Domain/Models/AttachmentFile.cs` | Added `PageCount` field (nullable int) |
| `QCS.Infrastructure/QCS.Infrastructure.csproj` | Added `PdfPig` NuGet reference (v0.1.10) |
| `QCS.Infrastructure/DependencyInjection.cs` | Registered `IPdfPageCounter` and `IPaperSavedService` |
| `QCS.Application/DependencyInjection.cs` | Registered `IPaperSavedService` in DI |
| `QCS.Application/Services/FileService.cs` | Count pages during PDF upload via `IPdfPageCounter` |
| `QCS.API/Controllers/DashboardController.cs` | Added 3 new endpoints for paper-saved metrics |
| `QCS.React.Admin/src/pages/overview/OverviewPage.tsx` | Added paper-saved KPI card + data fetch |
| `QCS.React.Admin/src/config/navigation.ts` | Added 'sustainability' to NavIcon type, added nav item |
| `QCS.React.Admin/src/components/layout/Sidebar.tsx` | Added SustainabilityIcon and case in renderIcon |
| `QCS.React.Admin/src/App.tsx` | Added `/sustainability` route |

### Migrations

| Name | Effect |
|------|--------|
| `20260520044803_AddPageCountToAttachmentFile` | Adds nullable `int PageCount` column to `AttachmentFiles` table |

---

## Testing & Validation

### Unit Tests (Recommended)
- Page counting with sample PDFs (valid, corrupt, non-PDF)
- Trend bucketing for all timeframe/aggregation combinations
- Backfill logic (success, failure, remaining count)

### Integration Tests (Recommended)
- API endpoints return correct DTOs with realistic data
- Summary reflects approved quotations with PageCount only
- Trend respects date ranges and aggregation windows
- Backfill updates database and returns accurate results

### Smoke Tests (Post-Deploy)
✅ Load Overview page → Paper Saved card renders with data
✅ Navigate to `/sustainability` → Page loads, displays trend chart
✅ Call `GET /api/Dashboard/PaperSaved` → Returns valid JSON with counts
✅ Call `GET /api/Dashboard/PaperSavedTrend?timeframe=6m&aggregation=month` → Returns monthly buckets
✅ Call `POST /api/Dashboard/BackfillPageCount?batchSize=100` → Returns result summary and updates database

---

## FAQ

**Q: Why store PageCount instead of extracting on-demand?**
A: PDF parsing is expensive (I/O + CPU). Storing on upload avoids re-parsing for every dashboard request. Backfill handles legacy data asynchronously without blocking the API.

**Q: What if PDF parsing fails?**
A: Sets `PageCount = 0` so the file is considered "processed" and won't be retried infinitely. Failed files are counted separately in backfill results for investigation.

**Q: Can I adjust the CO₂/water/tree constants?**
A: Yes, update constants in `PaperSavedService.cs` and redeploy. Calculation is real-time, so historical data will reflect new constants immediately.

**Q: Does this count ALL PDFs or just quotations?**
A: Only quotation PDFs from approved requests. Drafts, pending, and rejected requests are excluded from summaries.

**Q: When should I run backfill?**
A: After deployment, run it repeatedly until `Remaining: 0`. Then let the system count new uploads automatically going forward.

**Q: Can I run backfill multiple times?**
A: Yes, it's safe to run repeatedly. It only processes files where `PageCount IS NULL`, so already-counted files are skipped.

**Q: What happens if the database goes down during backfill?**
A: The backfill is transactional. Only files with successfully extracted page counts are committed. Restart the backfill to continue.

---

## Performance Considerations

- **Page Extraction:** Using PdfPig in-process is faster than external services but may spike CPU during backfill. Consider running backfill during off-hours.
- **Database:** Adding `PageCount` index on `AttachmentFile` is recommended if you query by this field frequently.
- **Caching:** Consider caching `GetSummaryAsync` results for 5–10 minutes to reduce database load.

---

## Related Files

- Backend Service: [`QCS.Application/Services/PaperSavedService.cs`](../QCS.Application/Services/PaperSavedService.cs)
- Page Counter: [`QCS.Infrastructure/Services/PdfPigPageCounter.cs`](../QCS.Infrastructure/Services/PdfPigPageCounter.cs)
- DTOs: [`QCS.Domain/DTOs/PaperSavedDto.cs`](../QCS.Domain/DTOs/PaperSavedDto.cs)
- API Endpoints: [`QCS.API/Controllers/DashboardController.cs`](../QCS.API/Controllers/DashboardController.cs)
- Frontend Dashboard: [`QCS.React.Admin/src/pages/sustainability/SustainabilityPage.tsx`](../QCS.React.Admin/src/pages/sustainability/SustainabilityPage.tsx)
- Overview Card: [`QCS.React.Admin/src/pages/overview/OverviewPage.tsx`](../QCS.React.Admin/src/pages/overview/OverviewPage.tsx)
- Navigation Config: [`QCS.React.Admin/src/config/navigation.ts`](../QCS.React.Admin/src/config/navigation.ts)

---

## Future Enhancements

1. **Export to ESG Report** — Generate PDF/Excel report for compliance filing
2. **Benchmarking** — Compare current period to previous periods (month-over-month, year-over-year)
3. **Department-Level Tracking** — Attribute savings by approver/requester department for internal accountability
4. **Gamification** — Leaderboard or milestones to incentivize digital workflow adoption
5. **Integration with Carbon Offset** — Calculate offset cost or tree planting equivalents for CSR programs
6. **Webhook Notifications** — Alert when sustainability milestones (e.g., 10,000 sheets saved) are reached
7. **Historical Snapshot** — Persist periodic snapshots to track growth over time

---

## Support & Troubleshooting

**Backfill endpoint returns 0 updated:**
- Check if attachments actually have PDF data (`Data IS NOT NULL`)
- Verify content type is set correctly (should contain "pdf")
- Check API logs for parse failures

**Sustainability page shows 0 sheets despite backfill completion:**
- Ensure at least one request is in Approved status
- Verify quotations have attachments with PageCount set
- Check database directly: `SELECT COUNT(*) FROM Quotations WHERE AttachmentFile.PageCount IS NOT NULL`

**PDF parsing is slow during backfill:**
- Normal for large/complex PDFs
- Consider running backfill overnight or in batches (adjust `batchSize` parameter)
- Monitor server CPU usage

