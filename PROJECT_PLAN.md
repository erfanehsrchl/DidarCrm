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
- **تصمیم:** Endpoint برابر `POST /api/v1/activities` است. Tenant از `X-Tenant-Id`، کلید درخواست از `Idempotency-Key` و `contactId` و `title` از JSON Body خوانده می‌شوند. مقدار `CreatedAt` در سرور و به‌صورت UTC تولید می‌شود. تصمیم دقیق Status Code و Header پاسخ به مرحله API موکول می‌شود.
- **دلیل:** Headerها اطلاعات Tenant و Idempotency را از Payload تجاری جدا می‌کنند. قرارداد Application نباید جزئیات پاسخ HTTP را در خود نگه دارد. Authentication خارج از محدوده است.

### D-009 — Concurrency مقدار LastActivityAt

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `LastActivityAt` فقط زمانی جلو می‌رود که Timestamp فعالیت جدید از مقدار ذخیره‌شده بزرگ‌تر باشد.
- **دلیل:** تراکنش‌های همزمان ممکن است برخلاف ترتیب ایجاد Commit شوند. Assignment ساده می‌تواند زمان آخرین فعالیت را به عقب برگرداند.

### D-010 — پیام‌رسانی Async

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** استفاده از RabbitMQ از طریق MassTransit 8 و Transactional Bus Outbox مبتنی بر EF Core. تنظیمات Queueهای Durable، Retry، Acknowledgement و تحویل Outbox از طریق قابلیت‌های MassTransit انجام می‌شوند.
- **دلیل:** نوشتن مستقیم در دیتابیس و Broker یک Failure Gap ایجاد می‌کند. Bus Outbox پیام را در تراکنش DbContext ذخیره و Delivery Service آن را پس از Commit به RabbitMQ تحویل می‌دهد؛ استفاده از پیاده‌سازی آزموده‌شده‌ی MassTransit ریسک ساخت دستی Polling، Lease و Publisher Confirm را حذف می‌کند.

### D-011 — توپولوژی اجرا

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** HTTP API، MassTransit Outbox Delivery Service، Notification Consumer و Webhook Consumer در یک ASP.NET Core Host اجرا می‌شوند.
- **دلیل:** برنامه یک Monolith با یک واحد استقرار باقی می‌ماند. SQL Server و RabbitMQ سرویس‌های زیرساختی مستقل هستند.

### D-012 — Eventها و Consumerها

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `ActivityCreatedEvent` روی یک Topic Exchange منتشر می‌شود و Notification و Webhook هرکدام Queue مستقل و Durable دارند. Versioning قرارداد تا زمانی که نیاز واقعی به تغییر Breaking ایجاد نشده، اضافه نمی‌شود.
- **دلیل:** هر Consumer باید Event را مستقل دریافت کند. Queueهای جدا Retry و Failure Handling آن‌ها را نیز از هم ایزوله می‌کنند.

### D-013 — Idempotency در Consumer

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** طراحی بر اساس At-least-once Delivery است. Consumer Outbox و `InboxState` استاندارد MassTransit برای Duplicate Detection و Commit اتمیک خروجی Consumer استفاده می‌شوند. ایجاد Notification همچنان با Unique Constraint محافظت می‌شود. Webhook Delivery یک شناسه ثابت، وضعیت ماندگار و Idempotency Header ارسالی به مقصد دارد.
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

- **وضعیت:** اصلاح‌شده توسط D-035
- **تصمیم:** MediatR، Generic Repository، CQRS Framework، AutoMapper یا Service Bus Abstraction اضافه نمی‌شوند، مگر اینکه در ادامه یک نیاز واقعی و قابل دفاع ایجاد شود.
- **دلیل:** کد صریح برای این تسک ساده‌تر بازبینی و دفاع می‌شود. MassTransit مطابق D-035 یک استثنای آگاهانه برای Messaging، Outbox و Inbox است.

### D-018 — نام Repository و Solution

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** نام Repository و Solution برابر `DidarCrm` و نام پروژه‌ها و Root Namespaceها به‌صورت `DidarCrm.*` است.
- **دلیل:** این نام هویت درخواستی پروژه را نشان می‌دهد و نام‌گذاری داخلی و خارجی را یکدست می‌کند.

### D-019 — زبان مستندات

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** مستندات توضیحی پروژه به زبان فارسی و با جهت راست‌به‌چپ نوشته می‌شوند؛ نام کلاس‌ها، قراردادها، Headerها، Endpointها و اصطلاحات فنی لازم به انگلیسی باقی می‌مانند.
- **دلیل:** مستندات باید برای بازبینی فعلی خوانا باشند، بدون اینکه دقت نام‌های فنی و قابلیت ارجاع به کد از بین برود.

### D-020 — نوع شناسه‌های اصلی

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** شناسه‌های Activity، Tenant و Contact از نوع `Guid` و به‌شکل Value Object تعریف می‌شوند.
- **دلیل:** شناسه‌های مستقل از دیتابیس ساخت Aggregate و Event را پیش از Persistence ممکن می‌کنند و از جابه‌جایی تصادفی شناسه‌های هم‌نوع جلوگیری می‌کنند.

### D-021 — محدودیت RequestKey و ActivityTitle

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `RequestKey` اجباری، حداکثر ۱۰۰ کاراکتر و یک مقدار Opaque است؛ Domain مقدار معتبر آن را تغییر نمی‌دهد. `ActivityTitle` اجباری، Trimشده و حداکثر ۵۰۰ کاراکتر است.
- **دلیل:** کلید Idempotency باید هویت دقیق درخواست را حفظ کند، درحالی‌که حذف فاصله‌های ابتدا و انتهای عنوان یک نرمال‌سازی نمایشی و قابل‌پیش‌بینی است.

### D-022 — ساخت CustomerActivity و زمان ایجاد

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `CustomerActivity` از طریق Factory ساخته می‌شود و شناسه Activity را هنگام ساخت تولید می‌کند. Application زمان UTC را از `IClock` اختصاصی می‌گیرد و Domain همان مقدار را بدون تبدیل یا Validation تکراری ذخیره می‌کند. Entity پس از ساخت فقط Getter دارد.
- **دلیل:** `IClock.UtcNow` منبع واحد زمان و مسئول تضمین UTC بودن آن است؛ Domain نباید همان قرارداد را دوباره بررسی یا زمان را به‌شکل پنهان تبدیل کند.

### D-023 — قرارداد CreateActivity در مرز Application

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `CreateActivityCommand` ورودی‌ها را با نوع‌های ساده `Guid` و `string` دریافت می‌کند و تبدیل آن‌ها به Value Object داخل Use Case انجام خواهد شد. `CreateActivityResponse` فقط شامل شناسه Activity و زمان ایجاد UTC است.
- **دلیل:** قرارداد Application نباید مدل HTTP باشد، مصرف‌کننده را مجبور به ساخت Value Objectهای Domain کند یا جزئیات Replay و Status Code مربوط به لایه API را در خروجی تجاری قرار دهد.

### D-024 — Portهای Persistence و مرز تراکنش

- **وضعیت:** منسوخ‌شده توسط D-028
- **تصمیم:** به‌جای Generic Repository از `IActivityRepository` و `IContactRepository` هدفمند استفاده می‌شود. مرز تراکنش با `ITransactionRunner.ExecuteAsync` بیان می‌شود و Implementation آن در Infrastructure مسئول Begin، Commit و Rollback خواهد بود.
- **دلیل:** Repositoryهای هدفمند زبان Use Case را بیان می‌کنند و `ITransactionRunner` بدون نشت API تراکنش EF Core، اتمیک بودن چند عملیات را در Application قابل مشاهده و تست می‌کند.

### D-025 — Result Pattern در مرز Use Case

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Use Caseها برای خطاهای قابل‌انتظار `Validation`، `NotFound` و `Conflict` از `Result<TValue>` استفاده می‌کنند. خطاهای غیرمنتظره Infrastructure، خطاهای برنامه‌نویسی و Cancellation همچنان Exception هستند.
- **دلیل:** خطاهای تجاری بخشی از قرارداد صریح Application می‌شوند و API می‌تواند بدون استفاده از Exception برای Control Flow آن‌ها را به پاسخ مناسب نگاشت کند.

### D-026 — قرارداد انتشار Integration Event

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `ActivityCreatedEvent` قرارداد مشترک `IIntegrationEvent` را پیاده‌سازی می‌کند و Application آن را از طریق `IIntegrationEventPublisher` منتشر می‌کند. نام Outbox یا RabbitMQ در Port لایه Application استفاده نمی‌شود و فعلاً قرارداد پیام Versioning ندارد.
- **دلیل:** Application قصد انتشار Event را بیان می‌کند، اما از مکانیزم Outbox و RabbitMQ آگاه نیست. Versioning قبل از وجود یک تغییر Breaking پیچیدگی غیرضروری ایجاد می‌کند.

### D-027 — جریان پایه CreateActivity

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `CreateActivityHandler` ورودی خام Command را به Value Object تبدیل می‌کند و خطاهای Validation و نبودن Contact را با `Result` برمی‌گرداند. جلو بردن `LastActivityAt`، درج Activity و انتشار Event پیش از یک فراخوانی `IUnitOfWork.SaveChangesAsync` انجام می‌شوند. رفتار Replay، Payload Conflict و Race درخواست‌های همزمان به مراحل بعد موکول شده‌اند.
- **دلیل:** مرز تراکنش باید کل تغییرات همگام و ثبت Event را پوشش دهد؛ در عین حال جدا نگه‌داشتن رفتارهای Idempotency، حجم هر مرحله را برای بازبینی محدود می‌کند.

### D-028 — DbContext به‌عنوان Unit of Work

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Application فقط به قرارداد کوچک `IUnitOfWork.SaveChangesAsync` وابسته است. `CrmDbContext` در Infrastructure باقی می‌ماند، این قرارداد را پیاده‌سازی می‌کند و تمام تغییرات Trackشده شامل Contact، Activity و Outbox را با یک `SaveChangesAsync` اتمیک ذخیره خواهد کرد. `ITransactionRunner` حذف می‌شود.
- **دلیل:** خود EF Core DbContext پیاده‌سازی طبیعی Unit of Work است. این طراحی Handler را از Lambda تراکنشی ساده‌تر می‌کند و در عین حال وابستگی Application به EF Core را به وجود نمی‌آورد.

### D-029 — رفتار Idempotent Replay و Payload Conflict

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** اگر برای ترکیب `(TenantId, RequestKey)` یک Activity موجود باشد، `ContactId` و `Title` نرمال‌شده با درخواست مقایسه می‌شوند. Payload یکسان پاسخ همان Activity را بدون هیچ Write یا Event جدید برمی‌گرداند؛ Payload متفاوت خطای `Activity.IdempotencyConflict` از نوع `Conflict` ایجاد می‌کند.
- **دلیل:** تکرار یک درخواست موفق نباید اثر جانبی تازه ایجاد کند و استفاده مجدد از همان کلید برای یک عملیات متفاوت نیز نباید به‌اشتباه موفق تلقی شود. کنترل Race میان دو Insert هم‌زمان همچنان بر عهده Unique Index و مرحله ۱.۱۰ است.

### D-030 — مدیریت Race مربوط به Idempotency Key

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Infrastructure برخورد با Unique Index مربوط به `(TenantId, RequestKey)` را به `DuplicateRequestKeyException` تبدیل می‌کند. Handler این Exception را فقط اطراف `SaveChangesAsync` دریافت می‌کند، Activity برنده را مجدداً می‌خواند و با همان قواعد مرحله ۱.۹ آن را به Replay یا Payload Conflict تبدیل می‌کند. اگر رکورد برنده پیدا نشود، Exception دوباره پرتاب می‌شود.
- **دلیل:** بررسی پیش از Insert به‌تنهایی در برابر درخواست‌های هم‌زمان امن نیست. Unique Index داور نهایی است و تبدیل خطای فنی آن در مرز Persistence مانع وابستگی Application به EF Core یا SQL Server می‌شود.

### D-031 — پایه EF Core و مالکیت DbContext

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `CrmDbContext` در Infrastructure قرار دارد، با EF Core 8 و Provider مربوط به SQL Server از طریق DI ثبت می‌شود و پیاده‌سازی Scoped قرارداد `IUnitOfWork` است. API فقط Connection String با نام `Database` را به Infrastructure تحویل می‌دهد. Connection String فعلی برای LocalDB محیط توسعه است و در مرحله Docker Compose با Configuration محیطی جایگزین می‌شود.
- **دلیل:** Application به EF Core وابسته نمی‌شود و Repositoryها و Unit of Work آینده در هر Request همان Instance از DbContext را به اشتراک خواهند گذاشت. این مرحله فقط زیرساخت اتصال را آماده می‌کند و هنوز هیچ Entity Mapping یا Migration ایجاد نمی‌کند.

### D-032 — نگاشت جدول‌های قدیمی

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `CustomerActivity` مستقیماً به جدول `crmActivity` و Entity دامنه `Contact` به جدول `cntContact` نگاشت می‌شوند و Value Objectهای آن‌ها با Value Converter ذخیره می‌شوند. ستون Domain با نام `CreatedAtUtc` به ستون قدیمی `CreatedAt` متصل است و Flagهای قدیمی `WebhookSent` و `NotificationCreated` به‌صورت Shadow Property حفظ می‌شوند. `RequestKey` برای سازگاری با رکوردهای قدیمی در Schema nullable باقی می‌ماند، هرچند ایجاد Activity جدید بدون آن در Domain ممکن نیست.
- **دلیل:** Activity و Contact در سیستم واقعی Domain Entity هستند؛ در این بازسازی فقط بخش موردنیاز Contact شامل شناسه، Tenant و `LastActivityAt` مدل می‌شود. نگه‌داشتن ستون‌های قدیمی در EF Model نیز از نادیده‌گرفتن ناخواسته آن‌ها در طراحی Migration جلوگیری می‌کند.

### D-033 — مدل محدود Contact در Domain

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `ContactRecord` از Infrastructure حذف و با Entity دامنه `Contact` جایگزین می‌شود. این Entity فقط `ContactId`، `TenantId` و `LastActivityAt` را دارد و کامل‌کردن سایر اطلاعات Contact خارج از محدوده‌ی این بازسازی است.
- **دلیل:** پروژه بخشی از یک CRM بزرگ‌تر را بازسازی می‌کند و Contact در سیستم واقعی یک Domain Entity است؛ ناقص‌بودن آگاهانه‌ی مدل در محدوده‌ی فعلی دلیل مناسبی برای تبدیل آن به مدل صرفاً Persistence نیست.

### D-034 — مدل ماندگار Outbox

- **وضعیت:** منسوخ‌شده توسط D-035
- **تصمیم:** پیام‌های آماده‌ی انتشار در جدول `outboxMessage` با شناسه Event، نوع، Payload سریال‌شده و زمان وقوع ذخیره می‌شوند. `PublishedAtUtc` وضعیت تکمیل، `AttemptCount` و `NextAttemptAtUtc` وضعیت Retry، `LockedUntilUtc` وضعیت Lease و `LastError` آخرین خطای انتشار را نگه می‌دارند. این مدل داخلی Infrastructure است و Domain Entity محسوب نمی‌شود.
- **دلیل:** Outbox یک مفهوم فنی برای حذف Failure Gap میان SQL و RabbitMQ است. نگهداری وضعیت Retry و Lease در همان رکورد، انتشار چند Instance را در مرحله Publisher قابل‌کنترل می‌کند، بدون آنکه جزئیات پیام‌رسانی وارد Domain شوند.

### D-035 — استفاده از MassTransit برای Messaging و Outbox

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** به‌جای Outbox، Polling، Lease و Publisher اختصاصی از MassTransit 8.4.0 استفاده می‌شود. Transactional Bus Outbox برای انتشار از API و Consumer Outbox برای Inbox و خروجی Consumerها به کار می‌روند. جداول استاندارد `InboxState`، `OutboxMessage` و `OutboxState` به `CrmDbContext` افزوده می‌شوند. `IIntegrationEventPublisher` در Application باقی می‌ماند و Infrastructure آن را با Adapter روی `IPublishEndpoint` پیاده‌سازی می‌کند.
- **دلیل:** پیاده‌سازی دستی Outbox شامل Serialization، Locking، Retry، Publisher Confirm، Cleanup و هماهنگی چند Instance است. MassTransit این رفتارها را یکپارچه و آزموده‌شده فراهم می‌کند و Adapter اجازه می‌دهد Application همچنان از Framework پیام‌رسانی مستقل بماند. نسخه ۸ برای سازگاری با .NET 8 و مجوز Apache-2.0 انتخاب شده است.

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

- **وضعیت:** پذیرفته‌شده
- افزودن `ActivityId`، `TenantId` و `ContactId`.
- افزودن تست ساخت، برابری و رد کردن مقدار خالی.

#### مرحله ۱.۲ — RequestKey و ActivityTitle

- **وضعیت:** پذیرفته‌شده
- افزودن دو Value Object و محدودیت طول/مقدار.
- افزودن تست Invariantهای همین دو نوع.

#### مرحله ۱.۳ — CustomerActivity و قرارداد زمان

- **وضعیت:** پذیرفته‌شده
- افزودن Entity، Factory و قرارداد `IClock`.
- افزودن تست ساخت و ذخیره بدون تغییر زمان UTC.

#### مرحله ۱.۴ — قرارداد ورودی و خروجی Use Case

- **وضعیت:** پذیرفته‌شده
- افزودن `CreateActivityCommand` و `CreateActivityResponse` بدون Handler.

#### مرحله ۱.۵ — Portهای Persistence و Transaction

- **وضعیت:** پذیرفته‌شده
- افزودن قراردادهای Repository و `IUnitOfWork`؛ بدون Implementation.

#### مرحله ۱.۶ — Result Pattern پایه

- **وضعیت:** پذیرفته‌شده
- افزودن `Error`، `ErrorType` و `Result<TValue>` همراه Unit Test.

#### مرحله ۱.۷ — قرارداد Integration Event

- **وضعیت:** پذیرفته‌شده
- تصمیم‌گیری و افزودن قرارداد Event و Port انتشار؛ بدون RabbitMQ یا Outbox Implementation.

#### مرحله ۱.۸ — جریان پایه CreateActivityHandler

- **وضعیت:** پذیرفته‌شده
- پیاده‌سازی مسیر موفق ثبت Activity.
- نگاشت Validation و نبودن Contact به `Result`.
- افزودن Unit Test برای همین سه مسیر؛ بدون منطق Replay یا Race.

#### مرحله ۱.۹ — Replay و Payload Conflict

- **وضعیت:** پذیرفته‌شده
- افزودن رفتار Idempotent Replay و خطای استفاده متفاوت از Key همراه تست.

#### مرحله ۱.۱۰ — Race مربوط به درخواست همزمان

- **وضعیت:** پذیرفته‌شده
- افزودن قرارداد خطای Duplicate Request Key در مرز Persistence.
- تبدیل بازنده Race به Replay یا Payload Conflict همراه Unit Test.
- تشخیص خطای SQL Server و تست Concurrency واقعی در مرحله Persistence انجام می‌شود.

### مرحله ۲ — SQL Persistence

#### مرحله ۲.۱ — EF Core و CrmDbContext

- **وضعیت:** پذیرفته‌شده
- افزودن Packageهای EF Core 8 و SQL Server.
- افزودن `CrmDbContext` خالی و اتصال آن به `IUnitOfWork`.
- ثبت DbContext در DI و خواندن Connection String از API.
- در این مرحله هنوز Entity Configuration، Repository و Migration وجود ندارد؛ این DbContext در مرحله ۲.۲ با نگاشت جدول‌های قدیمی تکمیل می‌شود.

#### مرحله ۲.۲ — نگاشت جدول‌های قدیمی

- **وضعیت:** پذیرفته‌شده
- نگاشت `CustomerActivity` و Value Objectهای آن به `crmActivity`.
- نگاشت Entity دامنه `Contact` به `cntContact`.
- حفظ Flagهای قدیمی به‌صورت Shadow Property.
- افزودن تست EF Model؛ بدون Repository، Migration یا اتصال واقعی به SQL Server.

#### مرحله ۲.۳ — نگاشت Outbox

- **وضعیت:** آماده‌بازبینی
- افزودن Package مربوط به MassTransit EF Core.
- افزودن Entityهای استاندارد `InboxState`، `OutboxMessage` و `OutboxState` به `CrmDbContext`.
- حذف مدل و Configuration اختصاصی Outbox.
- تکمیل تست EF Model؛ بدون اتصال RabbitMQ یا اجرای Delivery Service.

#### مرحله ۲.۳.۱ — Adapter انتشار Event

- **وضعیت:** شروع‌نشده
- پیاده‌سازی `IIntegrationEventPublisher` به‌عنوان Adapter روی `IPublishEndpoint`.
- ثبت MassTransit Bus Outbox روی `CrmDbContext`؛ Commit همراه Activity و Contact توسط `IUnitOfWork` انجام می‌شود.

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

### مرحله ۴ — RabbitMQ و MassTransit

#### مرحله ۴.۱ — Docker Compose زیرساخت محلی

- **وضعیت:** شروع‌نشده
- افزودن SQL Server و RabbitMQ و مستند اجرای محلی.

#### مرحله ۴.۲ — اتصال و توپولوژی RabbitMQ

- **وضعیت:** شروع‌نشده
- افزودن Transport مربوط به MassTransit RabbitMQ و تنظیم Exchange و Queueها.

#### مرحله ۴.۳ — تنظیم Outbox Delivery

- **وضعیت:** شروع‌نشده
- تنظیم Batch Size، Delivery Timeout، Retry و Concurrency مربوط به Bus Outbox.

#### مرحله ۴.۴ — Consumer Outbox و Retry

- **وضعیت:** شروع‌نشده
- تنظیم Consumer Outbox، Duplicate Detection Window و سیاست Retry/Redelivery.

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
- اتصال Consumer به Consumer Outbox و `InboxState` استاندارد MassTransit.

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
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۱.۱ شامل شناسه‌های Domain و تست‌های همان شناسه‌ها پیاده‌سازی شد؛ تصمیم D-020 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۱.۲ شامل `RequestKey`، `ActivityTitle` و تست Invariantهای آن‌ها پیاده‌سازی شد؛ تصمیم D-021 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۱.۳ شامل `CustomerActivity`، قرارداد اختصاصی `IClock` و تست ذخیره زمان UTC پیاده‌سازی شد؛ تصمیم D-022 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۱.۴ شامل قراردادهای `CreateActivityCommand` و `CreateActivityResult` پیاده‌سازی شد؛ تصمیم D-023 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۱.۵ شامل Repositoryهای هدفمند و `ITransactionRunner` پیاده‌سازی شد؛ تصمیم D-024 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۱.۶ شامل Result Pattern پایه و تغییر نام خروجی به `CreateActivityResponse` پیاده‌سازی شد؛ تصمیم D-025 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۱.۷ شامل `ActivityCreatedEvent` و Port عمومی `IIntegrationEventPublisher` پیاده‌سازی شد؛ نام‌گذاری و Versioning ساده نگه داشته شدند و تصمیم D-026 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۱.۸ شامل جریان پایه `CreateActivityHandler` و تست مسیر موفق، Validation و نبودن Contact پیاده‌سازی شد؛ تصمیم D-027 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | بنا بر بازبینی مرحله ۱.۸، `ITransactionRunner` با `IUnitOfWork` جایگزین شد؛ `CrmDbContext` در Infrastructure باقی ماند و تصمیم D-028 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۱.۹ شامل Idempotent Replay و Payload Conflict بدون اثر جانبی جدید پیاده‌سازی شد؛ تصمیم D-029 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۱.۱۰ شامل قرارداد Duplicate Request Key و مدیریت Replay یا Payload Conflict پس از باخت در Race پیاده‌سازی شد؛ تصمیم D-030 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۲.۱ شامل EF Core 8، `CrmDbContext`، اتصال `IUnitOfWork` و ثبت SQL Server در DI پیاده‌سازی شد؛ تصمیم D-031 ثبت شد. |
| ۱۴۰۵/۰۸/۲۸ | زیرمرحله ۲.۲ شامل نگاشت جدول‌های قدیمی `crmActivity` و `cntContact` و تست EF Model پیاده‌سازی شد؛ تصمیم D-032 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | بنا بر بازبینی مرحله ۲.۲، `ContactRecord` با Entity محدود `Contact` در Domain جایگزین شد؛ تصمیم D-033 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | زیرمرحله ۲.۳ شامل مدل و نگاشت ماندگار Outbox و وضعیت‌های انتشار، Retry و Lease پیاده‌سازی شد؛ تصمیم D-034 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | تصمیم Outbox اختصاصی بازنگری شد؛ مدل دستی با Entityهای استاندارد MassTransit جایگزین و مراحل Polling، Lease و Publisher Confirm دستی حذف شدند؛ تصمیم D-035 ثبت شد. |

</div>
