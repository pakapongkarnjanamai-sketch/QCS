# QCS — Quotation Compare System

ระบบบริหารจัดการใบเสนอราคา (Quotation) สำหรับงาน Procurement ภายในองค์กร  
รองรับกระบวนการเปรียบเทียบราคา, Workflow อนุมัติหลายขั้น, และสร้าง PDF เอกสารสุดท้ายแบบ Stamped อัตโนมัติ

---

## สารบัญ

- [ภาพรวมระบบ](#ภาพรวมระบบ)
- [โครงสร้างโปรเจค](#โครงสร้างโปรเจค)
- [Tech Stack](#tech-stack)
- [Domain Model](#domain-model)
- [กระบวนการทำงาน](#กระบวนการทำงาน)
- [ระบบยืนยันตัวตน](#ระบบยืนยันตัวตน)
- [Authorization Policies](#authorization-policies)
- [API Endpoints หลัก](#api-endpoints-หลัก)
- [Local Development](#local-development)
- [การ Deploy (IIS)](#การ-deploy-iis)

---

## ภาพรวมระบบ

```
┌──────────────────────────────────────────────────────────────┐
│                       IIS (Windows Server)                    │
│                                                              │
│  /QCS/admin    ──▶  QCS.React.Admin   (React SPA)           │
│  /QCS          ──▶  QCS.Web.User      (MVC User Portal)      │
│  /QCS/Service  ──▶  QCS.API           (ASP.NET Core API)     │
│  /PDF/Admin    ──▶  PDF.Admin         (MVC PDF Admin)        │
│  /PDF/Service  ──▶  PDF.Service       (PDF Merge/Stamp API)  │
└──────────────────────────────────────────────────────────────┘
```

| โปรเจค | บทบาท | ผู้ใช้งาน |
|--------|-------|-----------|
| `QCS.React.Admin` | Admin Portal (SPA) | IT Admin, Procurement Admin |
| `QCS.Web.User` | User Portal (MVC) | Requester, Approver ทั่วไป |
| `QCS.API` | Backend REST API | รับ request จากทุก frontend |
| `PDF.Admin` | PDF Management UI | Admin จัดการ Template |
| `PDF.Service` | PDF Merge & Stamp API | Internal service สร้าง PDF สุดท้าย |

---

## โครงสร้างโปรเจค

```
QCS/
├── QCS.API/                    # ASP.NET Core Web API (.NET 9)
│   ├── Controllers/            # HTTP endpoints (Request, Approval, User, Dashboard ...)
│   ├── Extensions/             # DI registration — Composition Root
│   ├── Middleware/             # GlobalExceptionHandler (ProblemDetails)
│   └── Security/               # AdminAccessClaimsTransformation
│
├── QCS.Application/            # Business Logic Layer
│   ├── Abstractions/           # Service interfaces (IRequestService, IQuotationService ...)
│   ├── Hubs/                   # SignalR NotificationHub
│   └── Services/               # Service implementations
│
├── QCS.Domain/                 # Domain Layer — ไม่ขึ้นกับชั้นอื่น
│   ├── Models/                 # Request, Quotation, ApprovalStep, User, AdminUserAccess
│   ├── DTOs/                   # Data Transfer Objects
│   └── Enum/                   # RequestStatus, AdminAccessLevel ...
│
├── QCS.Infrastructure/         # Data & External Services
│   ├── Data/                   # AppDbContext, Repository<T>, UnitOfWork
│   ├── Migrations/             # EF Core migrations
│   └── Services/               # Email, PDF, EmployeeLookup implementations
│
├── QCS.Web.Shared/             # Shared code สำหรับ MVC portals
│   ├── Middleware/             # ApiUserSyncMiddleware
│   └── Services/               # ApiUserService (sync Windows Identity → Roles)
│
├── QCS.React.Admin/            # React Admin SPA
│   └── src/
│       ├── config/             # appConfig.ts, navigation.ts
│       ├── components/         # AppLayout, Sidebar, reusable UI primitives
│       ├── lib/                # apiClient.ts, createDataSource.ts, toast.ts
│       └── pages/              # overview, requests, quotations, users, vendors, workflow
│
├── QCS.Web.User/               # ASP.NET Core MVC — User Portal
├── PDF.Admin/                  # ASP.NET Core MVC — PDF Admin
└── PDF.Service/                # ASP.NET Core Web API — PDF Processing
```

### Clean Architecture (QCS.API + Application + Infrastructure)

```
┌──────────┐   ◀──   ┌─────────────┐   ◀──   ┌────────────────┐   ◀──   ┌─────┐
│  Domain  │         │ Application │         │ Infrastructure │         │ API │
│  Models  │         │  Services   │         │ EF Core / HTTP │         │ MVC │
│  DTOs    │         │  Interfaces │         │ External APIs  │         │ DI  │
└──────────┘         └─────────────┘         └────────────────┘         └─────┘
```

- **Domain** — ไม่มี dependency ออกนอก layer นี้เลย
- **Application** — ขึ้นกับ Domain เท่านั้น; กำหนด interfaces, business rules
- **Infrastructure** — implement interfaces; EF Core, HttpClient, external integrations
- **API** — Composition Root; wire DI, controllers, middleware ไม่มี business logic

---

## Tech Stack

### Backend

| Technology | Version | บทบาท |
|------------|---------|--------|
| ASP.NET Core | .NET 9 | Web API + MVC Portals |
| C# | 13 | Primary language |
| Entity Framework Core | 9.x | ORM — SQL Server |
| Microsoft.AspNetCore.Authentication.Negotiate | - | Windows Auth (NTLM/Kerberos) |
| SignalR | Built-in | Real-time push notifications |
| DevExtreme.AspNet.Data | - | DataGrid server-side paging/filter/sort |
| IMemoryCache | Built-in | Role + user data caching |
| Swashbuckle (Swagger) | - | API documentation |

### Frontend — QCS.React.Admin

| Technology | Version | บทบาท |
|------------|---------|--------|
| React | 19 | UI Framework |
| TypeScript | ~6.0 | Strict type safety |
| Vite | 8 | Build tool + dev server |
| Tailwind CSS | 4 | Utility-first CSS (`@tailwindcss/vite`) |
| DevExtreme React | 24.2 | DataGrid, Chart, PieChart, TreeMap, Form |
| react-router-dom | 7 | SPA routing with `BrowserRouter` + `basename` |
| @microsoft/signalr | - | Real-time hub client |

### Infrastructure & Deployment

| Technology | บทบาท |
|------------|--------|
| SQL Server | Primary database |
| IIS (Windows Server) | Reverse proxy + static file serving |
| Windows Authentication | Domain-based identity (NTLM/Kerberos) |
| UNC File Share | PDF file storage with Windows impersonation |

---

## Domain Model

```
Request (ใบขอจัดซื้อ)
├── Code            — เลขเอกสาร
├── Title           — ชื่อรายการ
├── VendorCode      — รหัส Vendor
├── VendorName      — ชื่อ Vendor
├── RequestDate
├── ValidFrom / ValidUntil
├── Status          — 0=Draft, 1=Pending, 2=Approved, 9=Rejected
├── CurrentStepId
│
├── Quotations[]    (ไฟล์ใบเสนอราคา PDF)
│   ├── FileName, FilePath, ContentType, FileSize
│   ├── DocumentTypeId    — 10=Quotation, 20=Comparison, 30=Other
│   └── AttachmentFile    — binary (byte[]) เก็บใน DB
│
└── ApprovalSteps[] (ขั้นตอนอนุมัติ)
    ├── Sequence, StepName
    ├── ApproverNId, ApproverName
    ├── Status        — 0=Draft, 1=Pending, 2=Approved, 9=Rejected
    ├── ActionDate
    └── Comment
```

---

## กระบวนการทำงาน

### Business Flow หลัก

```
1. Requester สร้าง Request + อัปโหลด Quotation PDFs
         │
         ▼
2. เปรียบเทียบใบเสนอราคาตาม business rules
         │
         ▼
3. ส่ง Request เข้า Workflow อนุมัติตามลำดับขั้น
   ◀──── SignalR แจ้งเตือน Approver แบบ realtime ────▶
         │
         ▼  (ทุกขั้นอนุมัติแล้ว)
4. เรียก PDF.Service สร้างเอกสารสุดท้าย
         │
         ▼
5. ได้ PDF Official พร้อมตราอนุมัติทุกขั้น
```

### PDF Merge & Stamp (รายละเอียด)

```
IQuotationService.GenerateStampedPdfAsync(requestId)
    │
    ├── 1. โหลด Request + Quotations + ApprovalSteps จาก DB
    │
    ├── 2. สร้าง MergeAndStampRequestDto
    │         ├── documentName
    │         ├── files[]         ← PDF binary + DocumentTypeId ทุกไฟล์
    │         └── approvalSteps[] ← stamp metadata (NId, ชื่อ, วันที่) ทุกขั้น
    │
    └── 3. POST /api/Pdf/merge-stamp → PDF.Service
              └── Return: AttachmentResultDto (merged+stamped PDF binary)
```

---

## ระบบยืนยันตัวตน

ระบบใช้ **Windows Authentication (NTLM/Kerberos/Negotiate)** ทั้งหมด — **ไม่มี Login form**  
Browser ส่ง Windows Domain token ไปทุก request โดยอัตโนมัติ IIS จัดการ handshake

### Auth Flow ภาพรวม

```
Browser / React
    │  fetch(url, { credentials: 'include' })
    ▼
IIS — Windows Auth
    │  ตรวจสอบ DOMAIN\NId (เช่น NIKONOA\N1234)
    ▼
ASP.NET Core Negotiate Middleware
    │  ClaimsPrincipal ← Windows Identity
    ▼
AdminAccessClaimsTransformation          (QCS.API)
    │  Query AdminUserAccesses table → AccessLevel
    │  เพิ่ม Claims: qcs.nid, ClaimTypes.Role
    ▼
Authorization Policies
    │
    ▼
Controller / Endpoint
```

### ClaimsTransformation — QCS.API

`AdminAccessClaimsTransformation` ทำงานทุก request หลัง Windows Auth:

```
NIKONOA\N1234  →  ExtractNId()  →  "N1234"
                                        │
                            Query AdminUserAccesses
                                        │
                               AccessLevel: Manager
                                        │
                              ExpandRoles() — cumulative
                                        │
                              Claims เพิ่ม:
                                qcs.nid = "N1234"
                                Role = "User"
                                Role = "Manager"
```

**Role Hierarchy (Cumulative — แต่ละ level รับ role ล่างๆ ด้วย):**

| AccessLevel | Roles ที่ได้รับ |
|-------------|----------------|
| `User` | `User` |
| `Manager` | `Manager`, `User` |
| `Admin` | `Admin`, `Manager`, `User` |
| `SuperAdmin` | `SuperAdmin`, `Admin`, `Manager`, `User` |

> NId `N4734` → hard-coded SuperAdmin เสมอ (root account)

### ApiUserSyncMiddleware — QCS.Web.User

MVC Portal sync roles ผ่าน HTTP call ไปที่ QCS.API แทน ClaimsTransformation:

```
NIKONOA\N1234 (Windows Identity)
    │
    ▼  POST /api/users/windows-auth → QCS.API
UserDto + Roles[] กลับมา
    │
    ▼  สร้าง ClaimsPrincipal ใหม่
HttpContext.User = ClaimsPrincipal ที่มี Role claims
    │
    ▼  Cache 10 นาที (MemoryCache) เพื่อลด HTTP calls
```

### React Admin — Auth Pattern

React ไม่มี auth logic เอง — ส่ง `credentials: 'include'` ทุก request:

```typescript
// src/lib/apiClient.ts
export async function fetchWithAccessControl(input, init?) {
  const response = await fetch(input, init)   // ← init ต้องมี credentials: 'include'

  if (response.status === 403) {
    redirectToAccessDenied()                  // ← redirect ไป /access-denied
    throw new Error('Access denied.')
  }
  return response
}
```

> **Local Dev**: Vite proxy ไม่สามารถ relay NTLM ได้ — ต้องใช้ absolute URL ชี้ตรงที่ API  
> ตั้งค่าใน `.env.local`: `VITE_QCS_API_BASE_URL=https://localhost:7001`

---

## Authorization Policies

| Policy | Roles ที่ผ่านได้ | ใช้ที่ไหน |
|--------|----------------|-----------|
| `FallbackPolicy` (default ทุก endpoint) | Authenticated + `NIKONOA\` prefix | ทุก controller |
| `DomainUser` | Domain user ทุกคน | Endpoint ทั่วไป |
| `UserOrAbove` | User, Manager, Admin, SuperAdmin | Feature ทั่วไป |
| `ManagerOrAbove` | Manager, Admin, SuperAdmin | จัดการ workflow |
| `AdminOnly` | Admin, SuperAdmin | UserAccessController |
| `SuperAdminOnly` | SuperAdmin เท่านั้น | System config |

---

## API Endpoints หลัก

| Controller | Path | Method | บทบาท |
|------------|------|--------|--------|
| `SessionController` | `/api/Session/Me` | GET | ข้อมูล user ปัจจุบัน + roles |
| `RequestController` | `/api/Request/Admin/Draft` | GET | รายการ Requests (DataGrid) |
| `RequestController` | `/api/Request/Admin/Pending` | GET | รอ approve |
| `RequestController` | `/api/Request/Admin/Approved` | GET | Approved แล้ว |
| `ApprovalController` | `/api/Approval/Approve` | POST | อนุมัติ |
| `ApprovalController` | `/api/Approval/Reject` | POST | ปฏิเสธ |
| `QuotationController` | `/api/Quotation/{id}/Pdf` | GET | ดาวน์โหลด PDF |
| `UserAccessController` | `/api/UserAccess` | GET/POST/DELETE | จัดการสิทธิ์ Admin |
| `WorkflowController` | `/api/Workflow` | GET | Workflow routes |
| `VendorController` | `/api/Vendor` | GET | ข้อมูล Vendor |
| `DashboardController` | `/api/Dashboard/Summary` | GET | สรุปภาพรวม |
| `DashboardController` | `/api/Dashboard/RequestTrend` | GET | กราฟ trend |
| `EmployeeLookupController` | `/api/EmployeeLookup` | GET | ค้นหา Employee |

> DataGrid endpoints รับ `DataSourceLoadOptions` เป็น query params  
> และ return จาก `DataSourceLoader.Load()` โดยตรง (DevExtreme format)

---

## Local Development

### 1. Backend — QCS.API

สร้าง `QCS.API/appsettings.Development.json` (ไม่ถูก commit เข้า git):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=QCS;Trusted_Connection=True;"
  },
  "DomainSettings": {
    "DomainPrefix": "NIKONOA\\"
  },
  "CorsOrigins": [
    "https://localhost:5173",
    "http://localhost:5173"
  ],
  "PdfService": {
    "BaseUrl": "http://localhost:5200"
  }
}
```

รัน API:

```bash
cd QCS.API
dotnet run
```

### 2. Frontend — QCS.React.Admin

คัดลอก `.env.example` เป็น `.env.local` แล้วแก้ค่า:

```bash
cp QCS.React.Admin/.env.example QCS.React.Admin/.env.local
```

```bash
# .env.local
VITE_QCS_ADMIN_APP_BASE_PATH=/
VITE_QCS_API_BASE_URL=https://localhost:7001
VITE_QCS_HUB_URL=https://localhost:7001/hubs/qcs
VITE_QCS_PORTAL_BASE_URL=https://localhost:5000
```

รัน dev server:

```bash
cd QCS.React.Admin
npm install
npm run dev
```

> **สำคัญ**: `VITE_QCS_API_BASE_URL` ต้องเป็น absolute URL (https://localhost:...)  
> เพราะ Vite proxy ไม่สามารถ relay Windows Authentication (NTLM) ได้

---

## การ Deploy (IIS)

### โครงสร้าง IIS

```
Site Root (e.g. ap-server-01)
└── /QCS
    ├── /            → QCS.Web.User   (Windows Auth: On)
    ├── /Service     → QCS.API        (Windows Auth: On, CORS enabled)
    └── /Admin       → QCS.React.Admin (static SPA + URL Rewrite)
```

### Build Commands

```bash
# Build API
dotnet publish QCS.API -c Release -o ./publish/api

# Build React SPA
cd QCS.React.Admin
npm run build
# output → dist/
```

### web.config สำหรับ React SPA (URL Rewrite)

```xml
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="SPA fallback" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
          </conditions>
          <action type="Rewrite" url="/QCS/admin/index.html" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

> **สำคัญ**: ต้องใช้ absolute path `/QCS/admin/index.html` ไม่ใช่ relative path  
> เพราะ IIS `ExecuteURL` resolve relative path ผิด

---

## Real-time Notifications (SignalR)

QCS.API เปิด `NotificationHub` ที่ path `/notificationHub`  
เมื่อ Approval status เปลี่ยน Application layer จะ broadcast ให้ทุก client ที่ connect อยู่

```typescript
// React — เชื่อมต่อ Hub
const connection = new HubConnectionBuilder()
  .withUrl(appConfig.hubUrl, { withCredentials: true })
  .withAutomaticReconnect()
  .build()

connection.on('NotifyUpdates', () => {
  // trigger DataGrid refresh
})

await connection.start()
```

> Local Dev: ต้องตั้งค่า `VITE_QCS_HUB_URL` เป็น absolute URL  
> เพราะ Vite proxy ไม่รองรับ WebSocket Upgrade + NTLM handshake

---

## ไฟล์ที่ไม่ถูก Commit เข้า Git

ไฟล์เหล่านี้มี sensitive data — ต้องสร้างเองบนแต่ละเครื่อง/server:

| ไฟล์ | เหตุผล |
|------|--------|
| `*/appsettings.Development.json` | Connection strings, passwords |
| `*/appsettings.Production.json` | Production secrets |
| `QCS.React.Admin/.env.local` | API URLs, license keys |
| `*.pfx`, `*.pem`, `*.key` | SSL Certificates |

ใช้ [QCS.React.Admin/.env.example](QCS.React.Admin/.env.example) เป็น template สำหรับ frontend config
