# QCS User UX Standard

มาตรฐานนี้ใช้กับทุก operational page ใน `QCS.React.User` ทั้ง form, detail, lookup, list,
feedback และ modal หน้า Admin ใช้ DevExtreme และมี component contract แยกกัน

reference implementations คือ `RequestFormPage`, `QuotationDetailPage` และ `WorkspacePage`;
โครงสร้างกลางทั้งหมดอยู่ใน `QCS.React.User/src/components/ui`

## Page composition

เรียงส่วนประกอบของหน้าตามลำดับนี้:

1. `FormPageHeader` — ชื่อหน้า สถานะ คำอธิบายสั้น และลิงก์หรือ action ระดับหน้า
2. Error surface — อยู่ใต้ header และเหนือข้อมูล เพื่อให้เห็นทันที
3. `FormSummary` — ใช้เฉพาะตอนเปิดรายการเดิม แสดงข้อมูลสำคัญไม่เกิน 3 รายการต่อแถว
4. Context bar — ใช้เมื่อฟอร์มมี setup หรือบริบทที่เปลี่ยนความหมายของข้อมูล
5. `FormSection` — กลุ่มช่องกรอกข้อมูลหลัก
6. Focused sections — เช่น Documents และ Approval route ต้องเป็น sibling ของ `FormSection`
7. `FormActions` — action หลักด้านล่าง เรียง secondary ก่อน primary และ danger หลังสุด
8. Read-only history — แสดงหลัง action bar เมื่อรายการเข้าสู่ workflow แล้ว

ห้ามวาง bordered section ซ้อนใน `FormSection` เพราะจะเกิด card-in-card และทำให้ลำดับชั้นของ
ข้อมูลไม่ชัดเจน

## Hierarchy inside a section

โครงระดับหน้าเป็น section เรียงกันแบบ sibling เท่านั้น ลำดับชั้นที่ละเอียดกว่านั้น **สร้างข้างใน
section ไม่ใช่ด้วยการเพิ่มกรอบ** ใช้สามกลไกนี้ กลไกอื่นถือว่าเป็นการตกแต่ง

**1. Field group ด้วย `<fieldset>` + `<legend>`** — ใช้เมื่อหลาย field คือ *เรื่องเดียวกัน* ที่ถูก
แยกเป็นหลายช่อง reference คือ `VendorLookup` ซึ่งครอบ Code กับ Name ไว้ใต้ legend `VENDOR`

- legend ใช้ `text-caption font-medium uppercase tracking-[0.08em] text-ink-muted` และ fieldset
  ใช้ `grid gap-2`
- fieldset **ห้ามมี border, background หรือ padding** เป็นของตัวเอง มิฉะนั้นจะกลายเป็น card-in-card
- จัดกลุ่มเฉพาะเมื่อ field เป็นความคิดเดียวกันจริง การจัดกลุ่มเพื่อให้หน้าดูมีอะไรมากขึ้น แย่กว่า
  การไม่จัดกลุ่ม
- field ที่อยู่คนละกลุ่มกันห้ามถูกคั่นกลางด้วย field ของอีกกลุ่ม การวาง currency ไว้แถวหนึ่งแล้ว
  วาง amount ไว้อีกแถวโดยมี field ที่ไม่เกี่ยวข้องคั่น คือ hierarchy ที่ผิด ไม่ใช่แค่ layout

**2. Full-bleed band ในการ์ด** — ใช้เมื่อการ์ดหนึ่งใบมีโซนที่ทำหน้าที่ต่างจากเนื้อหาหลัก เช่น
action รองที่มี input ของตัวเอง reference คือแถบ Expired quotation code ใน `TypedDocumentEditor`

- band ใช้ `bg-surface-muted border-b border-border-subtle px-4 py-3` และ **กินเต็มความกว้างการ์ด**
- ห้าม inset ห้ามใส่ border ครบสี่ด้าน และห้ามใส่ radius — สามอย่างนี้ทำให้ band กลายเป็น card
  ซ้อน card ทันที
- band แบ่งการ์ดเป็นชั้นด้วย divider ไม่ใช่ด้วยกรอบ นี่คือความต่างที่สำคัญที่สุดของกลไกนี้
- ถ้าการ์ดไม่มีโซนที่สองจริง ๆ ก็ไม่ต้องมี band การเพิ่ม band ให้หน้าตาเหมือน reference คือการตกแต่ง

**3. Description ใต้ title ของ section** — `SectionCard` รับ `description` และมันไม่ใช่ของตกแต่ง
เสริม แต่คือที่สำหรับบอก *กฎที่ผู้ใช้ต้องรู้ ณ จุดที่เขากำลังทำสิ่งนั้น*

- เขียนเป็นประโยคสมบูรณ์ ไม่ใช่ป้ายกำกับ เช่น `PDF files only. Original Quotation is required when
  submitting.`
- section ที่พฤติกรรมไม่ตรงกับที่ผู้ใช้คาด **ต้องมี** description เช่น ไฟล์ที่ยังไม่อัปโหลดจน
  กว่าจะกด save
- section ที่ชัดเจนในตัวเองไม่ต้องมี description ที่ว่างดีกว่าข้อความที่ไม่ได้บอกอะไร

## Shared primitives

ใช้ component กลางต่อไปนี้แทนการคัดลอก Tailwind class:

- `FormPage` กำหนดความกว้าง `max-w-4xl` และระยะห่างแนวตั้งของหน้า
- `FormPageHeader` กำหนด title hierarchy และการ wrap ของ actions
- `FormSummary` กำหนดพื้นหลัง muted, responsive 3-column grid และขอบครบสี่ด้าน
- `FormSummaryItem` กำหนด label/value typography และรองรับค่าที่ยาวด้วย `truncate`
- `FormSection` กำหนด surface, border, radius, padding และ field rhythm
- `FormActions` กำหนดตำแหน่งและการ wrap ของปุ่มท้ายฟอร์ม
- `SectionCard` เป็น bordered section ที่มี title bar, description และ optional action; ใช้กับ
  Documents, Approval route, Approval steps และ detail sections แทนการเขียน shell ซ้ำ
- `LookupTableShell` เป็นเจ้าของ lookup title, search, first-load, refresh, error, empty และ footer
  placement โดย endpoint, DTO และ row rendering ยังเป็นของ feature
- `ExternalActionLink` เป็น external link ที่มี geometry/security/icon contract เหมือนกันทุกหน้า
- `LoadingSurface`, `EmptySurface`, `ErrorSurface` เป็น feedback surfaces กลางและใช้ semantic tokens
- `Field` กำหนด label, required marker และ inline validation
- `appInputClassName` / `appTextareaClassName` กำหนดรูปแบบ control
- `AppButton` / `IconButton` กำหนด action hierarchy และ focus state
- `AppLinkButton` ใช้กับ link ที่ทำหน้าที่เป็น action button เพื่อไม่ให้ padding และ height ต่างจาก
  ปุ่มจริง

## Button standard

- action ปกติใช้ `AppButton` ขนาด `md` (`min-h-11`); action รองหรือ action ใน panel ใช้ `secondary`
  หรือ `ghost` ตามความสำคัญ
- action ขนาดเล็กใช้ `size="sm"` (`min-h-9`); ห้ามสร้างปุ่ม action ด้วย `h-8` หรือ `py-1` เอง
- icon-only action ใช้ `IconButton` เสมอ ต้องมี `label` และใช้ size ที่กำหนด (`sm`, `md`, `lg`)
- link ที่หน้าตาเหมือนปุ่มใช้ `AppLinkButton`; link text ปกติยังใช้ anchor/link ธรรมดาได้
- ปุ่มที่มี icon และ text ใช้ `gap-2`; icon-only ไม่ต้องกำหนด gap เอง
- ทุกปุ่มต้องมี visible focus state และ disabled state; ห้ามใช้ raw button สำหรับ ordinary action
- raw button อนุญาตเฉพาะ selection controls เช่น summary filter, setup card, table sort และ suggestion
  row ซึ่งต้องกำหนด hit area และ focus state ให้ชัดเจน

## Reference composition

```tsx
<FormPage>
  <FormPageHeader
    title="New request"
    description="Save a draft at any time. Required fields apply when submitting."
    status={status}
    actions={headerActions}
  />

  {error && <ErrorSurface>{error.detail ?? error.title}</ErrorSurface>}

  {summary && (
    <FormSummary>
      <FormSummaryItem label="Requester" truncate>{summary.requester}</FormSummaryItem>
      <FormSummaryItem label="Request date">{summary.requestDate}</FormSummaryItem>
      <FormSummaryItem label="Current step" truncate>{summary.currentStep}</FormSummaryItem>
    </FormSummary>
  )}

  <FormSection>
    <Field label="Title" required error={errors.title}>
      <input className={appInputClassName('md', 'w-full')} />
    </Field>
  </FormSection>

  <DocumentsSection />

  <FormActions>
    <AppButton variant="secondary">Save draft</AppButton>
    <AppButton>Submit</AppButton>
  </FormActions>
</FormPage>
```

## Visual and responsive rules

- ใช้ semantic tokens เช่น `bg-surface-panel`, `border-border-subtle`, `text-ink-muted`; ห้ามเพิ่ม
  สีเฉพาะหน้าเมื่อ token เดิมสื่อความหมายได้
- ใช้ `rounded-sm` สำหรับ section, controls, selection surfaces และ links ที่มี focus state เพื่อให้
  geometry สม่ำเสมอ; `rounded-full` อนุญาตเฉพาะ indicator ที่ต้องสื่อความหมายเป็นวงกลม เช่น
  หมายเลขขั้นตอน workflow
- ใช้ spacing scale เดียวกัน: `space-y-6` ระหว่างกลุ่มระดับหน้า, `space-y-4` ภายใน form section,
  `gap-3` สำหรับ layout groups และ `gap-2` สำหรับ action groups
- control ปกติใช้ `h-9` และ control/action ขนาดเล็กใช้ `min-h-9`; ห้ามผสม `h-8` กับปุ่มมาตรฐาน
- ใช้ grid ที่เปลี่ยนเป็นหลายคอลัมน์ตั้งแต่ `sm` ขึ้นไป; mobile ต้องเรียงหนึ่งคอลัมน์โดยไม่เกิด
  horizontal page overflow
- action groups ต้อง `flex-wrap`; ห้ามกำหนดความกว้างคงที่ที่ทำให้ข้อความล้น
- ตารางในฟอร์มต้องมี `overflow-x-auto` และกำหนด `min-width` ให้คอลัมน์ยังอ่านได้
- validation อยู่ติดกับ field และ field ที่ผิดต้องมี `data-invalid="true"` เพื่อรองรับ
  focus-first-error
- header อยู่บนพื้นหน้าหลัก ไม่ครอบด้วย card
- ใช้สี semantic เป็นสัญญาณเล็ก ๆ เท่านั้น; danger สงวนไว้สำหรับ error และ destructive action

## Interaction rules

- ปุ่มที่ผู้ใช้กดเท่านั้นแสดง loading text; action อื่น disable ระหว่าง mutation
- ฟอร์มที่แก้ไขแล้วต้องมี `beforeunload` guard และ confirm ก่อนทิ้งข้อมูล
- draft validation กับ submit validation ต้องแยกกันเมื่อ draft อนุญาตให้ข้อมูลไม่ครบ
- error จาก API แสดงทั้ง error surface ระดับหน้าและ inline field error เมื่อ server ระบุ field ได้
- ห้ามใช้ `window.confirm`; ใช้ dialog ของแอป
- modal ต้อง focus ตัว dialog เมื่อเปิด, trap `Tab`/`Shift+Tab`, ปิดด้วย Escape และคืน focus ให้
  trigger เมื่อปิด
- render error boundary ต้องมี recovery action; ข้อความที่ไม่มี Reload/Retry/Back เป็น dead end

## Async and feedback standard

- first load ที่ยังไม่มีข้อมูลใช้ `LoadingSurface`; refresh หลังเคยโหลดสำเร็จต้องคงข้อมูลเดิมไว้
  พร้อม `aria-busy`, opacity และสถานะ Refreshing
- refresh failure ต้องคง last successful data พร้อมข้อความ `Showing the previous results.` และ
  ปุ่ม Retry
- retry เป็น user-triggered token ใน effect dependencies; ห้ามใช้ automatic retry timer
- error copy ต้องเป็นประโยคสมบูรณ์ก่อนต่อ recovery context
- feedback surfaces ใช้ `bg-surface-panel`, `bg-danger-soft`, `border-border-subtle` และ token ที่
  ประกาศใน `index.css`; feature code ห้ามสร้างชื่ออย่าง `bg-accent-subtle` หรือ `text-subheading`
  ถ้า theme ไม่ได้ประกาศไว้
- mutation ใช้ busy key แยกตาม action; action อื่น disable ได้ แต่มี loading state เฉพาะปุ่มที่กด

## Data-grid standard

ใช้กับ request workspace, selection tables, document tables และ lookup lists ใน QCS User:

- ครอบ table ด้วย `overflow-x-auto` และกำหนด `min-width` ตามจำนวนคอลัมน์ เพื่อให้มือถือเลื่อน
  ภายใน grid ได้โดยไม่เกิด page-level horizontal overflow
- ใช้ `w-full border-collapse text-left text-body` กับ table
- header และ body cell ใช้ `px-4 py-2.5`; ใช้ `whitespace-nowrap` เฉพาะ code, date และค่าที่ต้อง
  รักษาเป็นคอลัมน์เดียว
- ใช้ `border-b border-border-subtle bg-surface-muted` กับ header และ `divide-y divide-border-subtle`
  กับ body
- row ที่เลือกหรือ hover ต้องไม่เปลี่ยนขนาด layout; ใช้ background token เป็นสัญญาณแทนการเพิ่ม
  border
- pagination/footer อยู่ใน scroll container เดียวกันแต่ไม่อยู่ใน table และใช้ `border-t` กับ `pt-3`
- icon pagination/action ใช้ `rounded-sm` และ hit area อย่างน้อย `min-h-9` หรือ `p-1.5`
- ห้ามใช้ default `rounded` หรือ cell density เฉพาะจุดโดยไม่มีเหตุผลของ interaction

## Reuse in QRS

QRS นำ vocabulary นี้ไปใช้แบบ mirrored source files ไม่สร้าง shared npm package:

- ใช้ชื่อ component, props และ class contracts เดียวกัน (`FormPage`, `FormPageHeader`,
  `FormSection`, `FormActions`, `SectionCard`, `LookupTableShell`, `ExternalActionLink`)
- ใส่ comment ใน mirrored files ว่าต้นทางคือ QCS contract นี้ เพื่อให้ตรวจ divergence ได้
- business DTO, endpoint, validation และ row rendering ไม่ copy ข้ามระบบ; copy เฉพาะ composition
  และ interaction contracts
- QRS ต้องผ่าน audit invariant เดียวกัน: `mx-auto max-w-` อยู่ใน page primitive เท่านั้น,
  ordinary actions ไม่มี raw button, surfaces ใช้ semantic tokens และ grid scroll อยู่ภายใน
- **Hierarchy inside a section ใช้กับ QRS ด้วย** การ mirror เฉพาะ primitive ระดับ section ทำให้ได้
  โครงเหมือนกันแต่ข้างในแบนราบ ซึ่งเป็นสิ่งที่เกิดขึ้นจริงหลัง PLAN-066: โครงระดับหน้าตรงกันทุก
  ประการแล้ว แต่ QRS ไม่มี fieldset เลยสักอัน และใช้ `description` ของ `SectionCard` แค่ที่เดียว
  QRS จัดกลุ่ม field ด้วย `FieldGroup` ใน `components/ui` ส่วน QCS ยังใช้ `VendorLookup` แบบ inline
  ต่างกันได้ตราบใดที่ legend markup และ class ตรงกัน

## Verification contract

```powershell
npm run lint --prefix QCS.React.User
npm run build --prefix QCS.React.User
git diff --check
```

ตรวจ browser ที่ 1440x900 และ 390x844 พร้อมวัด ไม่ใช่ดูด้วยตาอย่างเดียว:

- page-level horizontal overflow เท่ากับ 0
- canonical page width บน desktop เท่ากับ 896px (`max-w-4xl`)
- wide tables มี `scrollWidth > clientWidth` เฉพาะ internal scroller บน mobile
- modal focus วนจาก last focusable กลับ first focusable
- stale rows ยังอยู่ระหว่าง refresh และหลัง refresh failure
- browser test ต้อง block และรายงาน non-GET requests; visual verification ต้องไม่สร้างข้อมูลธุรกิจ

เมื่อเพิ่มหน้าใหม่ ให้เริ่มจาก primitives ด้านบนและเพิ่ม component ใหม่เฉพาะเมื่อเป็นกลุ่มข้อมูล
ที่มีหน้าที่ชัดเจน ไม่คัดลอกโครงหน้าและ Tailwind class จาก feature page ไปทั้งชุด