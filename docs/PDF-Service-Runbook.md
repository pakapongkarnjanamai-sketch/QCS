# QCS PDF Merge and Stamp Service Runbook

Version: 1.0
Date: 2026-05-20
Status: Active operational runbook
Owner: QCS Application Team

## 1. Purpose

This document describes the QCS PDF generation flow for approved quotation packages. It covers the service boundary, endpoint contract, deployment paths, stamp rules, validation checklist, and lessons learned from the 2026-05-20 PDF service incident.

The goal is to keep PDF changes predictable. PDF output is official quotation evidence, so fixes must preserve page size, page content, stamp placement, and file readability across scanned, image-based, and vector PDF inputs.

## 2. System Overview

QCS final PDF generation is split across two applications:

1. `QCS.API`: loads quotation/request data, builds the merge-stamp payload, calls the PDF service, and returns the final PDF to portal users.
2. `PDF.Service`: receives PDF bytes and approval metadata, stamps each source PDF by document type, merges the stamped files, and returns the final PDF binary.

Primary user flow:

1. User opens `QCS.Web.User` quotation viewer at `/QCS/Quotation/View/{code}`.
2. Viewer calls `QCS.API` `/api/Quotation/ByCode/{code}` to load request metadata.
3. If request is fully approved, viewer calls `QCS.API` `/api/Quotation/ViewFile/{requestId}`.
4. `QCS.API` calls `PDF.Service` `/api/Pdf/merge-stamp`.
5. `PDF.Service` stamps and merges all quotation attachments.
6. `QCS.API` returns `application/pdf` to the viewer.

## 3. Runtime Endpoints

### 3.1 QCS.API

Approved final PDF:

```http
GET /api/Quotation/ViewFile/{requestId}
```

Source attachment preview:

```http
GET /api/Request/ViewFile/{quotationId}
```

Quotation metadata by code:

```http
GET /api/Quotation/ByCode/{code}
```

Preview merge before final approval:

```http
POST /api/Request/PreviewMergeStamp
```

### 3.2 PDF.Service

Merge and stamp endpoint:

```http
POST /api/Pdf/merge-stamp
Content-Type: application/json
Accept: application/pdf
```

Expected responses:

1. `200 OK application/pdf`: PDF generated successfully.
2. `400 Bad Request`: no PDF files were provided.
3. `500 Internal Server Error`: PDF.Service failed while stamping or merging.

`QCS.API` maps PDF service failures to `502 Bad Gateway` or `504 Gateway Timeout` where appropriate.

## 4. Configuration

Production PDF service URL in `QCS.API/appsettings.json`:

```json
"ExternalServices": {
  "PdfServiceUrl": "http://AP-NTC2137-PRWB/QCS/PDF"
}
```

Development PDF service URL in `QCS.API/appsettings.Development.json`:

```json
"ExternalServices": {
  "PdfServiceUrl": "https://localhost:7019"
}
```

`QuotationService` builds the final upstream endpoint by appending `/api/Pdf/merge-stamp` to `ExternalServices:PdfServiceUrl`.

## 5. Payload Contract

The payload sent by `QCS.Application.Services.QuotationService` uses camelCase JSON serialization.

```json
{
  "documentName": "QC-..._Title",
  "referenceCode": "QC-...",
  "pdfFiles": [
    {
      "name": "quotation.pdf",
      "documentTypeId": 10,
      "contentType": "application/pdf",
      "data": "base64 bytes",
      "length": 123456
    }
  ],
  "approvalData": {
    "name": "Vendor Name",
    "step": [
      {
        "stepName": "Prepared",
        "approver": "Approver Name",
        "approvalDate": "2026-05-20T12:00:00"
      }
    ]
  },
  "drawSetting": {
    "color": "#000000",
    "fontSize": 8,
    "margin": 20,
    "alignmentStamp": 2
  }
}
```

Important rules:

1. `pdfFiles[].data` must contain full PDF bytes loaded from `AttachmentFile.Data`.
2. `approvalData.step` must contain approved workflow steps only, ordered by workflow sequence.
3. `alignmentStamp = 2` means `TopRight` and is the QCS final PDF convention.
4. `PDF.Service` sorts input files by `DocumentTypeId` before stamping and merging.

## 6. Document Type and Stamp Rules

Current document type values are defined in `QCS.Domain.Enum.DocumentType`.

| DocumentTypeId | Display Name | Stamp Text | Color | Style |
| --- | --- | --- | --- | --- |
| 10 | ORIGINAL QUOTATION | ORIGINAL QUOTATION | Blue `#0000FF` with transparency | Detailed approval table |
| 20 | COMPARISON DOCUMENT | COMPARED | Red `#FF0000` | Compact header stamp |
| 30 | PRODUCT SPECIFICATIONS | SPECIFICATIONS | Black `#000000` | Compact header stamp |
| 40 | ATTACHMENT | ATTACHMENT | Black `#000000` | Compact header stamp |
| 50 | EXPIRED QUOTATION | EXPIRED | Red `#FF0000` | Compact header stamp |

Notes:

1. `EXPIRED QUOTATION` uses `DocumentTypeId = 50` only. A production check on 2026-05-20 found no `DocumentTypeId = 15` quotation records.
2. Do not reintroduce `15` unless a database migration or import path explicitly creates that value again.
3. Keep comparison and expired stamps compact. They should not include approval step details.

## 7. Alignment Values

`DrawSetting.AlignmentStamp` maps to `PDF.Service.Models.AlignmentStamp`.

| Value | Name |
| --- | --- |
| 0 | TopLeft |
| 1 | TopCenter |
| 2 | TopRight |
| 3 | CenterLeft |
| 4 | Center |
| 5 | CenterRight |
| 6 | BottomLeft |
| 7 | BottomCenter |
| 8 | BottomRight |

QCS final PDFs must default to `TopRight` (`2`). Avoid defaulting to `BottomRight` (`8`) because it moves approval stamps to the bottom of the document.

## 8. Implementation Notes

### 8.1 QCS.Application

`QuotationService.GenerateStampedPdfAsync`:

1. Loads request with quotations, attachment bytes, and approval steps.
2. Builds `MergeAndStampRequestDto`.
3. Sends the payload to `PDF.Service`.
4. Returns `Approved_{request.Code}.pdf`.

`CallMergeAndStampAsync`:

1. Uses a 30 second per-attempt timeout.
2. Retries transient errors with short delays.
3. Converts upstream failures to `PdfServiceException`.

### 8.2 PDF.Service

`PdfController.MergeAndStamp`:

1. Rejects empty file lists.
2. Sorts files by `DocumentTypeId`.
3. Calls `PdfGeneratorService.Stamp` for each file.
4. Calls `PdfGeneratorService.Merge` to append stamped PDFs into one output.

`PdfGeneratorService.Stamp` uses DevExpress PDF Graphics API to draw on existing PDF pages with `PdfGraphics.AddToPageForeground`. This is the safe path for preserving original PDF page size and content.

Do not use `PdfDocumentProcessor.RenderNewPage` to rebuild existing quotation pages unless the output is explicitly allowed to become a new layout. `RenderNewPage` is suitable for creating new pages, not for preserving source quotation pages byte-for-byte.

## 9. Deployment

Production target:

```text
\\10.10.154.21\wwwroot\QCS\PDF
```

Deployment script:

```powershell
PowerShell -ExecutionPolicy Bypass -File PDF.Service\scripts\Deploy-PDF-Service.ps1
```

If the default publish folder is locked, use a temporary publish path:

```powershell
PowerShell -ExecutionPolicy Bypass -File PDF.Service\scripts\Deploy-PDF-Service.ps1 `
  -PublishPath "c:\Users\n4734\source\repos\QCS\artifacts\publish\PDF.Service-temp"
```

The script:

1. Publishes `PDF.Service` in Release mode.
2. Backs up deployed config files.
3. Writes `app_offline.htm`.
4. Copies files with `robocopy /MIR`.
5. Removes `app_offline.htm`.
6. Health-checks `POST /api/Pdf/merge-stamp` via `HEAD`; `405 Method Not Allowed` is accepted as healthy because the endpoint is POST-only.

## 10. Post-Deploy Verification

Use these checks after every PDF-related deployment.

### 10.1 Service Health

```powershell
Invoke-WebRequest `
  -Uri "http://ap-ntc2137-prwb/QCS/PDF/api/Pdf/merge-stamp" `
  -Method Head `
  -UseDefaultCredentials
```

Expected result: `405 Method Not Allowed` or another explicit response from `PDF.Service`. A connection error means IIS routing or app pool startup failed.

### 10.2 QCS.API Final PDF

```powershell
Invoke-WebRequest `
  -Uri "https://ap-ntc2137-prwb/QCS/Service/api/Quotation/ViewFile/{requestId}" `
  -UseDefaultCredentials `
  -AllowUnencryptedAuthentication `
  -SkipHttpErrorCheck `
  -TimeoutSec 90
```

Check:

1. `StatusCode = 200`.
2. `Content-Type = application/pdf`.
3. `Content-Length` is in the expected range for the request.
4. The browser viewer shows all pages without right-side or bottom whitespace distortion.
5. Stamps are in the expected position and size.

Known production samples:

| Code | RequestId | Purpose |
| --- | --- | --- |
| QC-20260506-003 | 37 | Original plus expired quotation sample |
| QC-20260520-040 | 438 | Original plus comparison document sample |

## 11. Incident Notes from 2026-05-20

### 11.1 502 Bad Gateway

Symptom:

```text
GET /QCS/Service/api/Quotation/ViewFile/{id} -> 502 Bad Gateway
```

Cause:

`QCS.API` could not call `PDF.Service` at the configured production URL.

Fix:

1. Deploy `PDF.Service` to `\\10.10.154.21\wwwroot\QCS\PDF`.
2. Ensure `ExternalServices:PdfServiceUrl` points to `http://AP-NTC2137-PRWB/QCS/PDF`.
3. Recycle/redeploy `QCS.API` if required.

### 11.2 Stamp Moved to BottomRight

Symptom:

Approval stamp appeared at the bottom right instead of the top right.

Cause:

`DrawSetting.AlignmentStamp` used `8`, which maps to `BottomRight`.

Fix:

Use `2`, which maps to `TopRight`, for final quotation PDFs.

### 11.3 Rasterized Page Regression

Symptom:

Some final PDFs showed extra right-side and bottom whitespace, distorted page sizing, and oversized stamps.

Cause:

An attempted fix rasterized existing pages and rebuilt them with `RenderNewPage`. This changed how original PDF content was represented and scaled.

Resolution:

Reverted to drawing stamps on existing pages with `PdfGraphics.AddToPageForeground`.

Rule:

Do not rasterize existing quotation pages or rebuild final quotation pages with `RenderNewPage` as a general stamping fix.

### 11.4 Expired Stamp Follow-Up

Requirement:

Expired quotation documents should use a red `EXPIRED` compact stamp, similar to comparison documents.

Safe state:

The document type/profile rule can be changed safely without rasterizing pages. If the stamp does not render for a specific source PDF, investigate DevExpress coordinate system or annotation alternatives while preserving the original page.

## 12. Troubleshooting Guide

### 12.1 Final PDF Returns 502

Check:

1. `PDF.Service` IIS app exists under `/QCS/PDF`.
2. App pool is running.
3. `QCS.API/appsettings.json` `PdfServiceUrl` points to the production PDF app.
4. `PDF.Service` endpoint responds to a `HEAD` request with `405`.

### 12.2 PDF Opens but Stamp Is Missing

Check:

1. The attachment has the expected `DocumentTypeId`.
2. `DrawSetting.AlignmentStamp` is `2` for `TopRight`.
3. The stamp is not being drawn outside the visible crop box.
4. DevExpress drawing uses `AddToPageForeground` on the existing page.
5. Browser cache is bypassed; `QCS.API` should return no-cache headers for generated PDFs.

### 12.3 PDF Layout Changes After a Fix

Immediately check for these anti-patterns:

1. `CreateBitmap` used to flatten source pages.
2. `RenderNewPage` used to recreate source quotation pages.
3. Manual page rectangle creation that ignores original crop/media boxes.
4. DPI conversion changes without visual regression testing.

If any of these are present, revert before investigating stamp-specific fixes.

## 13. Development Checklist

Before changing PDF stamp behavior:

1. Test `QC-20260506-003` and `QC-20260520-040` or equivalent samples.
2. Verify page count remains unchanged.
3. Verify page orientation and visible bounds remain unchanged.
4. Verify file size does not grow unexpectedly due to rasterization.
5. Verify original, expired, and comparison stamp profiles separately.
6. Read DevExpress Office File API documentation for `PdfGraphics`, coordinate systems, and `AddToPageForeground` before changing drawing mode.
7. Deploy first to a non-production path or local test output when possible.

## 14. References

Important source files:

1. `QCS.Application/Services/QuotationService.cs`
2. `QCS.Domain/DTOs/PdfServiceDto.cs`
3. `QCS.Domain/Enum/DocumentType.cs`
4. `QCS.API/Controllers/QuotationController.cs`
5. `QCS.API/Controllers/RequestController.cs`
6. `PDF.Service/Controllers/PdfController.cs`
7. `PDF.Service/Models/PdfRequestModel.cs`
8. `PDF.Service/Services/PdfGeneratorService.cs`
9. `PDF.Service/scripts/Deploy-PDF-Service.ps1`

DevExpress documentation:

1. PDF Graphics API: `https://docs.devexpress.com/OfficeFileAPI/119009`
2. `PdfGraphics.AddToPageForeground`: `https://docs.devexpress.com/OfficeFileAPI/DevExpress.Pdf.PdfGraphics.AddToPageForeground(DevExpress.Pdf.PdfPage)`