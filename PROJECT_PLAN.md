<div dir="rtl" align="right">

# برنامه زنده پروژه بازطراحی Activity در Didar CRM

این سند مرجع اصلی محدوده پروژه، تصمیم‌های معماری، ترتیب پیاده‌سازی و وضعیت بازبینی است. هر زمان تصمیمی تغییر کند، این فایل نیز باید به‌روزرسانی شود. تصمیم‌های جایگزین‌شده بدون ردپا حذف نمی‌شوند؛ وضعیت آن‌ها به `منسوخ‌شده` تغییر می‌کند و تصمیم جدید همراه با دلیل ثبت می‌شود.

## ۱. صورت مسئله

Endpoint قدیمی ثبت Activity در CRM در مسیر هر درخواست چهار مسئولیت را انجام می‌دهد:

1. یک Activity ایجاد می‌کند.
2. مقدار `LastActivityAt` مخاطب را به‌روزرسانی می‌کند.
3. یک اعلان داخلی می‌سازد.
4. یک Webhook خارجی را فراخوانی می‌کند.

این وابستگی مستقیم، Endpoint را کند و نگهداری و عملیات آن را دشوار کرده است. سیستم در محیط واقعی فعال است، چند میلیون رکورد دارد و امکان توقف طولانی آن وجود ندارد. بازطراحی باید داده‌ها را حفظ کند، مهاجرت تدریجی را ممکن سازد و در مدتی که نسخه قدیم و جدید همزمان اجرا می‌شوند امن باقی بماند.

جداول فعلی:

<div dir="ltr" align="left">

```text
crmActivity
(Id, TenantId, ContactId, Title, CreatedAt,
 WebhookSent, NotificationCreated)

cntContact
(Id, TenantId, LastActivityAt)
```

</div>

## ۲. محدوده پروژه

راهکار نهایی این موارد را پیاده‌سازی می‌کند:

- ثبت Idempotent فعالیت بر اساس ترکیب `(TenantId, RequestKey)`.
- ثبت اتمیک Activity، جلو بردن زمان آخرین فعالیت مخاطب و درج پیام Outbox.
- ایجاد Async اعلان و ارسال Webhook از طریق RabbitMQ.
- الگوی Transactional Outbox و Consumerهای Idempotent با استفاده از Inbox و Deduplication.
- نگهداری وضعیت پردازش خارج از جدول `crmActivity`.
- Migration سازگار با نسخه قبلی و Backfill دسته‌ای، قابل توقف، قابل ادامه و قابل اجرای مجدد.
- تست خودکار برای Concurrency، تحویل تکراری پیام، قطع Broker و اجرای مجدد Backfill.
- Health Check، ابزارهای عیب‌یابی و روش بررسی صحت مهاجرت.

موارد خارج از محدوده:

- پیاده‌سازی یک CRM کامل
- احراز هویت کاربران
- مدیریت Tenantها
- رابط کاربری اعلان‌ها
- پیاده‌سازی سرویس مقصد واقعی Webhook

## ۳. معماری

برنامه یک Monolith ساده با مرزهای Clean Architecture و یک ASP.NET Core Host قابل استقرار است.

<div dir="ltr" align="left">

```text
DidarCrm.Api
    |-- HTTP Endpoints
    |-- Outbox BackgroundService
    `-- RabbitMQ Consumer BackgroundServices
             |
             v
DidarCrm.Application  <----  DidarCrm.Infrastructure
             |                         |
             v                         |-- EF Core / SQL Server
DidarCrm.Domain                 `-- RabbitMQ / HTTP Clients
```

</div>

وابستگی‌های مجاز بین پروژه‌ها:

<div dir="ltr" align="left">

```text
Api            -> Application, Infrastructure
Infrastructure -> Application, Domain
Application    -> Domain
Domain         -> بدون وابستگی به پروژه‌های دیگر
```

</div>

قوانین تجاری و Use Caseها نباید به ASP.NET Core، EF Core، RabbitMQ یا جزئیات زیرساخت وابسته باشند.

## ۴. ساختار Repository

<div dir="ltr" align="left">

```text
DidarCrm/
|-- src/
|   |-- DidarCrm.Domain/
|   |-- DidarCrm.Application/
|   |-- DidarCrm.Infrastructure/
|   `-- DidarCrm.Api/
|-- tests/
|   |-- DidarCrm.UnitTests/
|   `-- DidarCrm.IntegrationTests/
|-- scripts/
|   `-- backfill/                 # در مرحله Backfill اضافه می‌شود
|-- docker-compose.yml            # در مرحله زیرساخت اضافه می‌شود
|-- README.md
|-- ARCHITECTURE.md               # خروجی نهایی و خلاصه معماری، حداکثر دو صفحه
`-- PROJECT_PLAN.md               # همین سند زنده
```

</div>

Migrationهای EF Core در مسیر زیر قرار می‌گیرند:

<div dir="ltr" align="left">

```text
DidarCrm.Infrastructure/Persistence/Migrations
```

</div>

اسکریپت‌های Backfill عملیات حجیم و Set-based هستند و جداگانه در مسیر `scripts/backfill` نگهداری می‌شوند.

## ۵. تاریخچه تصمیم‌ها

### D-001 — Runtime و Framework

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** استفاده از .NET 8 و ASP.NET Core 8.
- **دلیل:** انتخاب صریح پروژه و یک نسخه LTS مناسب برای این تسک.

### D-002 — شکل برنامه

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** پیاده‌سازی یک Monolith ساده؛ نه Microservice و نه Modular Monolith.
- **دلیل:** تمرکز پیاده‌سازی و بازبینی روی مسائل اصلی باقی می‌ماند و مرزهای Clean Architecture همچنان قوانین تجاری را از زیرساخت جدا می‌کنند.

### D-003 — پروژه‌های Clean Architecture

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** استفاده از چهار پروژه `Domain`، `Application`، `Infrastructure` و `Api`.
- **دلیل:** این چهار مرز برای اندازه فعلی پروژه کافی هستند. پروژه‌های بیشتر بدون بهبود طراحی اصلی، تشریفات اضافه ایجاد می‌کنند.

### D-004 — Persistence

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** استفاده از SQL Server و EF Core 8 برای عملیات عادی برنامه و SQL هدفمند برای Backfill حجیم.
- **دلیل:** EF Core نگاشت‌ها و Migrationها را مدیریت می‌کند؛ اما انتقال چند میلیون رکورد به Batchهای کوتاه، Set-based و قابل ادامه نیاز دارد، نه Materialize کردن Entityها.

### D-005 — Repository و Transaction Abstraction

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** استفاده از Repositoryهای هدفمند مانند `IActivityRepository` و `IContactRepository` همراه با Unit of Work یا Transaction Abstraction. Generic Repository ایجاد نمی‌شود و `IQueryable` از Infrastructure خارج نخواهد شد.
- **دلیل:** Repository هدفمند نیاز واقعی Use Case را بیان می‌کند. Generic Repository عمدتاً EF Core را تکرار می‌کند و جزئیات Persistence را نشت می‌دهد.

### D-006 — مرز تراکنش ثبت Activity

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** درج Activity، به‌روزرسانی Monotonic مقدار `LastActivityAt` و درج Outbox در یک تراکنش SQL Commit می‌شوند.
- **دلیل:** این بخش مرز Strong Consistency موردنیاز مسئله است. هر Activity ثبت‌شده باید یک Event ماندگار و آماده انتشار داشته باشد.

### D-007 — Idempotency درخواست

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Header با نام `Idempotency-Key` اجباری است. یکتایی ترکیب `(TenantId, RequestKey)` با Filtered Unique Index در SQL Server تضمین می‌شود. `RequestKey` فقط برای رکوردهای قدیمی Nullable است و برای رکوردهای جدید حداکثر ۱۰۰ کاراکتر دارد.
- **دلیل:** بررسی وجود رکورد پیش از Insert در شرایط همزمان Race Condition دارد؛ دیتابیس باید مرجع نهایی Concurrency باشد.

### D-008 — قرارداد API ثبت Activity

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Endpoint برابر `POST /api/v1/activities` است. Tenant از `X-Tenant-Id`، کلید درخواست از `Idempotency-Key` و `contactId` و `title` از JSON Body خوانده می‌شوند. مقدار `CreatedAt` در سرور و به‌صورت UTC تولید می‌شود. درخواست جدید `201 Created` و درخواست تکراری `200 OK` همراه با `Idempotent-Replay: true` دریافت می‌کند.
- **دلیل:** Headerها اطلاعات Tenant و Idempotency را از Payload تجاری جدا و رفتار Replay را شفاف می‌کنند. Authentication خارج از محدوده است.

### D-009 — Concurrency مقدار LastActivityAt

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `LastActivityAt` فقط زمانی جلو می‌رود که Timestamp فعالیت جدید از مقدار ذخیره‌شده بزرگ‌تر باشد.
- **دلیل:** تراکنش‌های همزمان ممکن است برخلاف ترتیب ایجاد Commit شوند. Assignment ساده می‌تواند زمان آخرین فعالیت را به عقب برگرداند.

### D-010 — پیام‌رسانی Async

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** استفاده از RabbitMQ با Queueهای Durable، پیام‌های Persistent، Publisher Confirm و Manual Acknowledgement. انتشار فقط از طریق Transactional Outbox انجام می‌شود.
- **دلیل:** نوشتن مستقیم در دیتابیس و Broker یک Failure Gap ایجاد می‌کند. Outbox اجازه می‌دهد هنگام قطع RabbitMQ، API موفق بماند و پیام پس از بازیابی Broker منتشر شود.

### D-011 — توپولوژی اجرا

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** HTTP API، Outbox Publisher، Notification Consumer و Webhook Consumer در یک ASP.NET Core Host و به‌صورت Hosted Background Service اجرا می‌شوند.
- **دلیل:** برنامه یک Monolith با یک واحد استقرار باقی می‌ماند. SQL Server و RabbitMQ سرویس‌های زیرساختی مستقل هستند.

### D-012 — Eventها و Consumerها

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Event نسخه‌دار `ActivityCreatedV1` روی یک Topic Exchange منتشر می‌شود و Notification و Webhook هرکدام Queue مستقل و Durable دارند.
- **دلیل:** هر Consumer باید Event را مستقل دریافت کند. Queueهای جدا Retry و Failure Handling آن‌ها را نیز از هم ایزوله می‌کنند.

### D-013 — Idempotency در Consumer

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** طراحی بر اساس At-least-once Delivery است. ثبت Inbox و ایجاد Notification به‌صورت اتمیک Commit و با Unique Constraint محافظت می‌شوند. Webhook Delivery یک شناسه ثابت، وضعیت ماندگار و Idempotency Header ارسالی به مقصد دارد.
- **دلیل:** RabbitMQ ممکن است یک پیام را دوباره تحویل دهد. تضمین Exactly-once برای اثر جانبی HTTP بدون همکاری سرویس مقصد ممکن نیست.

### D-014 — راهبرد Migration

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** استفاده از الگوی Expand/Contract با ترتیب: گسترش Schema، استقرار کد سازگار، Backfill دسته‌ای، Reconciliation، فعال‌سازی مسیر Async، مانیتورینگ و در نهایت حذف رفتار و ستون‌های قدیمی.
- **دلیل:** نسخه قدیم و جدید باید بدون توقف یا از دست رفتن داده همزمان اجرا شوند.

### D-015 — رفتار Backfill

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** استفاده از Batch مبتنی بر Keyset، تراکنش‌های کوتاه، Checkpoint ماندگار و Unique Constraint. اجرای مجدد باید امن باشد. Flagهای تکمیل‌شده قدیمی به وضعیت Completed و رکوردهای ناقص به Pending تبدیل و با نرخ کنترل‌شده آزاد می‌شوند.
- **دلیل:** Offset Scan و یک تراکنش بزرگ برای میلیون‌ها رکورد و نوشتن همزمان Production امن نیستند.

### D-016 — تست

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** استفاده از xUnit و اجرای تست‌های Integration روی SQL Server و RabbitMQ واقعی با Testcontainers. برای تست Concurrency یا رفتار Relational از EF Core InMemory استفاده نمی‌شود.
- **دلیل:** Unique Constraint، تراکنش، Locking و Redelivery پیام رفتارهای اصلی این سیستم هستند و باید روی Dependency واقعی تست شوند.

### D-017 — پرهیز از Frameworkهای غیرضروری

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** MediatR، Generic Repository، CQRS Framework، AutoMapper یا Service Bus Abstraction اضافه نمی‌شوند، مگر اینکه در ادامه یک نیاز واقعی و قابل دفاع ایجاد شود.
- **دلیل:** کد صریح برای این تسک ساده‌تر بازبینی و دفاع می‌شود.

### D-018 — نام Repository و Solution

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** نام Repository و Solution برابر `DidarCrm` و نام پروژه‌ها و Root Namespaceها به‌صورت `DidarCrm.*` است.
- **دلیل:** این نام هویت درخواستی پروژه را نشان می‌دهد و نام‌گذاری داخلی و خارجی را یکدست می‌کند.

### D-019 — زبان مستندات

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** مستندات توضیحی پروژه به زبان فارسی و با جهت راست‌به‌چپ نوشته می‌شوند؛ نام کلاس‌ها، قراردادها، Headerها، Endpointها و اصطلاحات فنی لازم به انگلیسی باقی می‌مانند.
- **دلیل:** مستندات باید برای بازبینی فعلی خوانا باشند، بدون اینکه دقت نام‌های فنی و قابلیت ارجاع به کد از بین برود.

## ۶. مراحل پیاده‌سازی

هر زیرمرحله یک Review Gate و Commit مستقل دارد. کار زیرمرحله بعد نباید با زیرمرحله فعلی مخلوط شود. در پایان هر زیرمرحله، خلاصه تغییرات، نتیجه Build و Test و هر تصمیم تغییرکرده ارائه می‌شود و سپس برای ادامه منتظر بازبینی می‌مانیم.

وضعیت‌های قابل استفاده: `شروع‌نشده`، `درحال‌انجام`، `آماده‌بازبینی`، `پذیرفته‌شده`.

### مرحله ۰ — Bootstrap و برنامه زنده

- **وضعیت:** پذیرفته‌شده
- ساخت Solution مبتنی بر .NET 8 و پروژه‌های Clean Architecture.
- تنظیم Project Referenceها و تنظیمات پایه Build.
- حذف کد نمونه Template.
- ایجاد Git Repository مستقل.
- ثبت تصمیم‌ها و مراحل قابل بازبینی در این سند.

**تمرکز بازبینی:** ساختار Repository، جهت Dependencyها و کامل بودن تاریخچه تصمیم‌ها.

### مرحله ۱ — Domain و Use Case ثبت Activity

#### مرحله ۱.۱ — شناسه‌های Domain

- **وضعیت:** شروع‌نشده
- افزودن `ActivityId`، `TenantId` و `ContactId`.
- افزودن تست ساخت، برابری و رد کردن مقدار خالی.

#### مرحله ۱.۲ — RequestKey و ActivityTitle

- **وضعیت:** شروع‌نشده
- افزودن دو Value Object و محدودیت طول/مقدار.
- افزودن تست Invariantهای همین دو نوع.

#### مرحله ۱.۳ — Entity مربوط به CustomerActivity

- **وضعیت:** شروع‌نشده
- افزودن Entity و Factory ساخت آن.
- افزودن تست ساخت و نرمال‌سازی زمان UTC.

#### مرحله ۱.۴ — قرارداد ورودی و خروجی Use Case

- **وضعیت:** شروع‌نشده
- افزودن `CreateActivityCommand` و `CreateActivityResult` بدون Handler.

#### مرحله ۱.۵ — Portهای Persistence و Time

- **وضعیت:** شروع‌نشده
- افزودن قراردادهای Repository، Clock و Transaction؛ بدون Implementation.

#### مرحله ۱.۶ — قرارداد Integration Event

- **وضعیت:** شروع‌نشده
- تصمیم‌گیری و افزودن قرارداد Event و Port انتشار؛ بدون RabbitMQ یا Outbox Implementation.

#### مرحله ۱.۷ — Happy Path مربوط به CreateActivityHandler

- **وضعیت:** شروع‌نشده
- پیاده‌سازی فقط مسیر موفق ثبت Activity و تست آن.

#### مرحله ۱.۸ — Replay و Payload Conflict

- **وضعیت:** شروع‌نشده
- افزودن رفتار Idempotent Replay و خطای استفاده متفاوت از Key همراه تست.

#### مرحله ۱.۹ — Race مربوط به درخواست همزمان

- **وضعیت:** شروع‌نشده
- مدیریت Conflict دیتابیس در سطح Application Contract و تست مسیر Race.

### مرحله ۲ — SQL Persistence

#### مرحله ۲.۱ — EF Core و CrmDbContext

- **وضعیت:** شروع‌نشده
- افزودن Packageها، DbContext خالی و ثبت Dependency Injection.

#### مرحله ۲.۲ — نگاشت جدول‌های قدیمی

- **وضعیت:** شروع‌نشده
- نگاشت `crmActivity` و `cntContact` بدون Repository.

#### مرحله ۲.۳ — نگاشت Outbox

- **وضعیت:** شروع‌نشده
- افزودن مدل Persistence و Mapping جدول Outbox.

#### مرحله ۲.۴ — Repositoryهای Activity و Contact

- **وضعیت:** شروع‌نشده
- پیاده‌سازی Repositoryها و تست Integration پایه.

#### مرحله ۲.۵ — UnitOfWork و مرز تراکنش

- **وضعیت:** شروع‌نشده
- پیاده‌سازی تراکنش اتمیک و تست Rollback.

#### مرحله ۲.۶ — Migration و Indexهای Idempotency

- **وضعیت:** شروع‌نشده
- ایجاد Migration سازگار و Filtered Unique Index.

#### مرحله ۲.۷ — تست Concurrency در SQL Server

- **وضعیت:** شروع‌نشده
- تست درخواست همزمان و Monotonic بودن `LastActivityAt` با Testcontainers.

### مرحله ۳ — HTTP API

#### مرحله ۳.۱ — Endpoint و DTOها

- **وضعیت:** شروع‌نشده
- افزودن `POST /api/v1/activities` فقط برای Happy Path.

#### مرحله ۳.۲ — Validation و Problem Details

- **وضعیت:** شروع‌نشده
- افزودن Validation Header/Body و Error Mapping.

#### مرحله ۳.۳ — پاسخ Replay و تست Endpoint

- **وضعیت:** شروع‌نشده
- افزودن `201`/`200` و Integration Testهای HTTP.

### مرحله ۴ — RabbitMQ و Outbox Publisher

#### مرحله ۴.۱ — Docker Compose زیرساخت محلی

- **وضعیت:** شروع‌نشده
- افزودن SQL Server و RabbitMQ و مستند اجرای محلی.

#### مرحله ۴.۲ — اتصال و توپولوژی RabbitMQ

- **وضعیت:** شروع‌نشده
- افزودن Connection، Exchange و Queue declaration بدون Publisher Loop.

#### مرحله ۴.۳ — Polling و Lease مربوط به Outbox

- **وضعیت:** شروع‌نشده
- خواندن امن Batch پیام‌ها و مدیریت Lease.

#### مرحله ۴.۴ — Publish Confirm و Retry

- **وضعیت:** شروع‌نشده
- انتشار پیام، Confirm و Backoff.

#### مرحله ۴.۵ — تست قطع Broker

- **وضعیت:** شروع‌نشده
- بررسی ثبت Activity هنگام قطعی و انتشار پس از بازیابی.

### مرحله ۵ — Notification Consumer

#### مرحله ۵.۱ — مدل و Schema اعلان

- **وضعیت:** شروع‌نشده
- افزودن وضعیت پردازش اعلان خارج از `crmActivity`.

#### مرحله ۵.۲ — Consumer مسیر موفق

- **وضعیت:** شروع‌نشده
- مصرف Event و ایجاد یک Notification.

#### مرحله ۵.۳ — Inbox و Deduplication

- **وضعیت:** شروع‌نشده
- افزودن Inbox و Commit اتمیک.

#### مرحله ۵.۴ — تست Redelivery

- **وضعیت:** شروع‌نشده
- اثبات جلوگیری از اعلان تکراری.

### مرحله ۶ — Webhook Delivery

#### مرحله ۶.۱ — مدل Delivery و HTTP Port

- **وضعیت:** شروع‌نشده
- افزودن وضعیت Webhook و قرارداد Sender.

#### مرحله ۶.۲ — ارسال موفق Webhook

- **وضعیت:** شروع‌نشده
- پیاده‌سازی Happy Path با Idempotency Header.

#### مرحله ۶.۳ — Retry و Backoff

- **وضعیت:** شروع‌نشده
- مدیریت خطای موقت و تعداد تلاش.

#### مرحله ۶.۴ — خطای دائمی و Dead Letter

- **وضعیت:** شروع‌نشده
- ثبت شکست نهایی و تست بازیابی.

### مرحله ۷ — Migration و Backfill

#### مرحله ۷.۱ — طراحی Schema انتقال

- **وضعیت:** شروع‌نشده
- نهایی‌سازی نگاشت Flagهای قدیمی به وضعیت جدید.

#### مرحله ۷.۲ — Backfill اعلان

- **وضعیت:** شروع‌نشده
- اسکریپت Batch و قابل اجرای مجدد برای Notification.

#### مرحله ۷.۳ — Backfill وب‌هوک

- **وضعیت:** شروع‌نشده
- اسکریپت Batch و قابل اجرای مجدد برای Webhook.

#### مرحله ۷.۴ — Checkpoint و Resume

- **وضعیت:** شروع‌نشده
- افزودن توقف/ادامه و تست اجرای مجدد.

#### مرحله ۷.۵ — Concurrent Write و Reconciliation

- **وضعیت:** شروع‌نشده
- تست نوشتن همزمان و Queryهای صحت‌سنجی.

### مرحله ۸ — Operability

#### مرحله ۸.۱ — Health Check

- **وضعیت:** شروع‌نشده
- افزودن Liveness و Readiness.

#### مرحله ۸.۲ — Structured Logging

- **وضعیت:** شروع‌نشده
- افزودن Correlation و Logهای کلیدی.

#### مرحله ۸.۳ — Metrics

- **وضعیت:** شروع‌نشده
- افزودن شاخص‌های Outbox، Queue، Retry و Backfill.

#### مرحله ۸.۴ — Graceful Shutdown

- **وضعیت:** شروع‌نشده
- تست توقف امن Publisher و Consumer.

### مرحله ۹ — تحویل نهایی

#### مرحله ۹.۱ — README اجرایی

- **وضعیت:** شروع‌نشده
- تکمیل راه‌اندازی، Migration، Test و Failure Drill.

#### مرحله ۹.۲ — ARCHITECTURE.md

- **وضعیت:** شروع‌نشده
- تهیه مستند حداکثر دوصفحه‌ای موردنیاز تسک.

#### مرحله ۹.۳ — بازبینی نهایی

- **وضعیت:** شروع‌نشده
- اجرای تست کامل، بررسی Secret و کنترل پوشش نیازمندی‌ها.

## ۷. پروتکل بازبینی

برای هر مرحله:

1. فقط محدوده همان مرحله پیاده‌سازی شود.
2. Build و Test مرتبط اجرا شوند.
3. وضعیت مرحله و تصمیم‌های تغییرکرده در همین سند به‌روزرسانی شوند.
4. فایل‌های تغییرکرده، رفتار، نتایج تست و ریسک‌های حل‌نشده ارائه شوند.
5. پیش از آغاز مرحله بعد، نتیجه بازبینی دریافت شود.

## ۸. تاریخچه تغییرات سند

| تاریخ | تغییر |
|---|---|
| ۱۴۰۵/۰۸/۲۸ | سند زنده ایجاد و تصمیم‌های مربوط به .NET 8، Monolith ساده، Clean Architecture، Repository، SQL Server، RabbitMQ، Outbox/Inbox، Migration، Backfill و تست ثبت شدند. |
| ۱۴۰۵/۰۸/۲۸ | نام Repository، Solution، پروژه‌ها و Root Namespaceها به `DidarCrm` تغییر کرد. |
| ۱۴۰۵/۰۸/۲۸ | زبان سند به فارسی تغییر کرد و تصمیم D-019 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | چینش مستندات راست‌به‌چپ و راست‌چین شد؛ بلوک‌های کد چپ‌به‌راست باقی ماندند. |
| ۱۴۰۵/۰۸/۲۸ | مراحل پیاده‌سازی به زیرمرحله‌های کوچک و دارای Commit و Review Gate مستقل تقسیم شدند. |

</div>
