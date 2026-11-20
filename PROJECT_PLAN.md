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

- **وضعیت:** تکمیل‌شده توسط D-048
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
- **تصمیم:** `ActivityCreatedEvent` روی یک Fanout Exchange منتشر می‌شود و Notification و Webhook هرکدام Queue مستقل و Durable دارند. Versioning قرارداد تا زمانی که نیاز واقعی به تغییر Breaking ایجاد نشده، اضافه نمی‌شود.
- **دلیل:** هر دو Consumer باید تمام Eventهای ایجاد Activity را مستقل دریافت کنند و در حال حاضر هیچ نیاز واقعی به Routing Key یا فیلتر پیام وجود ندارد. Queueهای جدا Retry و Failure Handling آن‌ها را نیز از هم ایزوله می‌کنند.

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

- **وضعیت:** اصلاح‌شده توسط D-048
- **تصمیم:** `CreateActivityCommand` ورودی‌ها را با نوع‌های ساده `Guid` و `string` دریافت می‌کند و تبدیل آن‌ها به Value Object داخل Use Case انجام خواهد شد. `CreateActivityResult` فقط شامل شناسه Activity و زمان ایجاد UTC است.
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

- **وضعیت:** اصلاح‌شده توسط D-040
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

- **وضعیت:** اصلاح‌شده توسط D-040
- **تصمیم:** `CrmDbContext` در Infrastructure قرار دارد و با EF Core 8 و Provider مربوط به SQL Server از طریق DI ثبت می‌شود. API فقط Connection String با نام `Database` را به Infrastructure تحویل می‌دهد. Connection String فعلی برای LocalDB محیط توسعه است و در مرحله Docker Compose با Configuration محیطی جایگزین می‌شود. پیاده‌سازی مستقیم `IUnitOfWork` بعداً مطابق D-040 از DbContext جدا شد.
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

### D-036 — Adapter انتشار Event روی MassTransit

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `MassTransitIntegrationEventPublisher` قرارداد `IIntegrationEventPublisher` را با استفاده از `IPublishEndpoint` پیاده‌سازی می‌کند و پیام را با Runtime Type واقعی آن Publish می‌کند. Application همچنان هیچ Referenceای به MassTransit ندارد. فعال‌سازی Bus Outbox همراه با RabbitMQ Transport در مرحله ۴.۲ انجام می‌شود.
- **دلیل:** اگر Event با نوع Compile-time برابر `IIntegrationEvent` منتشر شود، قرارداد عمومی Interface می‌تواند به‌جای Event مشخص استفاده شود. ارسال Runtime Type تضمین می‌کند `ActivityCreatedEvent` قرارداد واقعی پیام باقی بماند. جداکردن Adapter از تنظیم Transport نیز حجم این مرحله را محدود نگه می‌دارد.

### D-037 — پیاده‌سازی ActivityRepository

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `ActivityRepository` برای خواندن Activity بر اساس `(TenantId, RequestKey)` از `AsNoTracking` و `SingleOrDefaultAsync` استفاده می‌کند. افزودن Activity فقط آن را به Change Tracker همان `CrmDbContext` اضافه می‌کند و Repository مستقیماً `SaveChangesAsync` اجرا نمی‌کند.
- **دلیل:** Query بدون Tracking به Handler اجازه می‌دهد بعد از شکست Unique Index، رکورد برنده‌ی Commit‌شده را از دیتابیس بخواند و Entity بازنده‌ی موجود در Change Tracker نتیجه را آلوده نکند. `SingleOrDefaultAsync` نیز نقض احتمالی تضمین یکتایی را پنهان نمی‌کند. Commit باید در `IUnitOfWork` باقی بماند تا Activity، Contact و Outbox اتمیک باشند.

### D-038 — به‌روزرسانی Monotonic مربوط به Contact

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Entity دامنه `Contact` مسئول جلو بردن `LastActivityAt` است و مقدار مساوی یا قدیمی‌تر را نادیده می‌گیرد. `ContactRepository` فقط Contact را با Tracking و بر اساس Tenant و Contact Id می‌خواند؛ `CreateActivityHandler` نبودن Contact را مدیریت و رفتار Domain را فراخوانی می‌کند. ستون `LastActivityAt` در EF Core یک Concurrency Token است.
- **دلیل:** قانون رو به جلو بودن زمان یک Invariant دامنه است و Application باید اجرای این رفتار را به‌صورت صریح Orchestrate کند؛ Repository فقط مسئول بازیابی Aggregate است. Tracking باعث می‌شود تغییر همراه Activity و Outbox در یک `SaveChangesAsync` ذخیره شود و Concurrency Token از Lost Update خاموش جلوگیری می‌کند. Resolve و Retry مربوط به `DbUpdateConcurrencyException` در مرحله ۲.۵ تکمیل خواهد شد.

### D-039 — Resolve کردن Contact Concurrency در Unit of Work

- **وضعیت:** اصلاح‌شده توسط D-041
- **تصمیم:** پیاده‌سازی `IUnitOfWork.SaveChangesAsync` فقط `DbUpdateConcurrencyException`هایی را Resolve می‌کند که تمام Entryهای آن‌ها `Contact` باشند. وضعیت فعلی دیتابیس دوباره روی Entity بارگذاری و قصد قبلی Request با فراخوانی مجدد `Contact.AdvanceLastActivityAt` اعمال می‌شود؛ کل `SaveChanges` حداکثر سه بار تلاش می‌شود. حذف Contact، Conflict مربوط به Entity دیگر یا تمام‌شدن تعداد تلاش‌ها بدون تبدیل دوباره پرتاب می‌شود.
- **دلیل:** این Retry قانون Monotonic را در شرایط Commit هم‌زمان حفظ می‌کند و چون SaveChanges ناموفق به‌صورت اتمیک Rollback می‌شود، Activity و Outbox در تلاش بعدی همراه Contact دوباره ذخیره می‌شوند. محدودکردن نوع Conflict و تعداد تلاش از پنهان‌شدن خطاهای واقعی یا Retry بی‌نهایت جلوگیری می‌کند.

### D-040 — جداسازی EfUnitOfWork از CrmDbContext

- **وضعیت:** اصلاح‌شده توسط D-041
- **تصمیم:** `CrmDbContext` فقط مسئول EF Model و Change Tracking است و دیگر `IUnitOfWork` را پیاده‌سازی نمی‌کند. `EfUnitOfWork` مستقل مسئول Save، Retry و ترجمه‌ی خطاهای Persistence است. هنگام Contact Concurrency، Infrastructure فقط State دیتابیس را Reload می‌کند و تصمیم Monotonic با اجرای مجدد متد Domain گرفته می‌شود.
- **دلیل:** با اضافه‌شدن Retry و ترجمه خطا، قرار دادن Policyهای Persistence داخل DbContext مسئولیت آن را بیش از حد گسترده می‌کرد. همچنین مقایسه مستقیم Timestampها در Infrastructure قانون تجاری Domain را تکرار می‌کرد. جداسازی جدید DbContext را ساده و قانون انتخاب زمان را فقط در `Contact` نگه می‌دارد.

### D-041 — Orchestration مربوط به Contact Concurrency در Application

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `EfUnitOfWork` هیچ شناختی از Contact ندارد و فقط `DbUpdateConcurrencyException` را به `PersistenceConcurrencyException` عمومی تبدیل می‌کند. `CreateActivityHandler` با Retry محدود، از `IContactRepository.ReloadAsync` برای Refresh فنی Entity استفاده و سپس رفتار `Contact.AdvanceLastActivityAt` را دوباره اجرا می‌کند.
- **دلیل:** تشخیص اینکه Conflict این Use Case قابل Retry است و اجرای دوباره رفتار Contact بخشی از Workflow کاربردی است، نه مسئولیت DbContext یا Unit of Work عمومی. Repository فقط عملیات Persistence مربوط به Reload را پنهان می‌کند و قانون انتخاب Timestamp صرفاً در Domain باقی می‌ماند.

### D-042 — ترجمه خطای Unique Index مربوط به Request Key

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `EfUnitOfWork` فقط خطاهای SQL Server با شماره `2601` یا `2627` را که نام Index برابر `UX_crmActivity_TenantId_RequestKey` دارند به `DuplicateRequestKeyException` تبدیل می‌کند. سایر `DbUpdateException`ها بدون تغییر منتشر می‌شوند. نام Index به‌صورت Constant مشترک با Configuration نگهداری می‌شود.
- **دلیل:** شماره‌های 2601 و 2627 می‌توانند برای Unique Constraintهای دیگری نیز رخ دهند؛ بررسی نام Index مانع تبدیل اشتباه خطاهای نامرتبط به Replay می‌شود. Application فقط مفهوم باخت در Race مربوط به Request Key را می‌شناسد و به SQL Server وابسته نمی‌شود.

### D-043 — Filtered Unique Index مربوط به Idempotency

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** EF Model یک Unique Index ترکیبی روی `(TenantId, RequestKey)` با نام `UX_crmActivity_TenantId_RequestKey` و Filter برابر `[RequestKey] IS NOT NULL` دارد. Configuration و Error Translation از یک Constant مشترک برای نام Index استفاده می‌کنند.
- **دلیل:** یکتایی درخواست‌های جدید باید در دیتابیس تضمین شود، اما میلیون‌ها Activity قدیمی می‌توانند `RequestKey = NULL` داشته باشند. Filter باعث می‌شود رکوردهای قدیمی خارج از محدودیت باشند و در عین حال Race مربوط به کلیدهای جدید توسط SQL Server حل شود.

### D-044 — Migration افزایشی روی Schema قدیمی

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** نخستین Migration پروژه یک Migration از نوع Expand است و وجود جدول‌های قدیمی `crmActivity` و `cntContact` را پیش‌فرض می‌گیرد. این Migration فقط ستون Nullable مربوط به `RequestKey` را به `crmActivity` اضافه می‌کند، جداول استاندارد MassTransit را می‌سازد و Filtered Unique Index را ایجاد می‌کند. متد `Down` نیز فقط همین اجزای جدید را حذف می‌کند و هرگز جدول‌های قدیمی را Drop نمی‌کند. ابزار EF از `CrmDbContextFactory` مستقل از Host استفاده می‌کند.
- **دلیل:** ساخت دوباره یا حذف جدول‌های قدیمی می‌تواند داده‌های موجود را از بین ببرد. Factory زمان طراحی نیز اجازه می‌دهد Migrationها پیش از تکمیل ثبت RabbitMQ و `IPublishEndpoint` تولید شوند، بدون اینکه رفتار Runtime یا مرزهای Clean Architecture تغییر کند.

### D-045 — تست تراکنش روی SQL Server واقعی

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** تست‌های وابسته به رفتار اختصاصی SQL Server با `Testcontainers.MsSql` و Image مشخص SQL Server 2022 اجرا می‌شوند. تست Rollback ابتدا Schema قدیمی را ایجاد و Expand Migration را واقعاً اعمال می‌کند؛ سپس با ایجاد خطای Unique Index در یک `SaveChangesAsync` تأیید می‌کند که تغییر Contact، Activity جدید و Outbox Message همگی Rollback می‌شوند.
- **دلیل:** Providerهای In-Memory یا SQLite رفتار تراکنش، Filtered Index و خطاهای SQL Server را دقیق بازتولید نمی‌کنند. این تست فعلاً اتمیک‌بودن Persistence را اثبات می‌کند؛ ثبت خودکار Event توسط MassTransit Bus Outbox پس از فعال‌سازی آن در مرحله ۴.۲ جداگانه تست خواهد شد.

### D-046 — تست قطعی Contact Concurrency

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** سناریوی هم‌زمانی با دو Scope و دو `CrmDbContext` واقعی اجرا می‌شود. هر دو Handler پیش از Save روی یک Barrier هم‌زمان می‌شوند؛ سپس ترتیب Save عمداً طوری کنترل می‌شود که Activity جدیدتر ابتدا Commit و درخواست قدیمی‌تر با مقدار Concurrency منقضی‌شده ذخیره شود. تست دو بار فراخوانی Unit of Work درخواست بازنده، موفقیت هر دو درخواست، ثبت هر دو Activity و باقی‌ماندن بزرگ‌ترین `LastActivityAt` را تأیید می‌کند.
- **دلیل:** اجرای آزاد دو Task ممکن است بدون ایجاد Conflict هم سبز شود و یک تست Flaky یا مثبت کاذب بسازد. ترتیب قطعی ثابت می‌کند مسیر واقعی `DbUpdateConcurrencyException`، Reload و Retry لایه Application اجرا شده است.

### D-047 — قرارداد HTTP و تشخیص Replay

- **وضعیت:** منسوخ‌شده توسط D-048
- **تصمیم:** `POST /api/v1/activities` برای ایجاد جدید پاسخ `201 Created` همراه Header استاندارد `Location` و برای Replay یک درخواست یکسان پاسخ `200 OK` می‌دهد. JSON پاسخ در هر دو حالت فقط `activityId` و `createdAtUtc` دارد. Application نتیجه را با `CreateActivityOutcome` شامل نوع `Created` یا `Replayed` به API اعلام می‌کند، اما این اطلاعات در DTO پاسخ HTTP نمایش داده نمی‌شود. خطاهای ورودی با `400 ValidationProblem` و خطاهای `NotFound` و `Conflict` با RFC 7807 و Statusهای `404` و `409` برگردانده می‌شوند. پیاده‌سازی `IClock` از `DateTimeOffset.UtcNow` استفاده می‌کند.
- **دلیل:** Status Code باید ایجاد واقعی را از Replay جدا کند، بدون اینکه جزئیات Idempotency وارد Payload عمومی شود. Problem Details نیز قرارداد خطای یکنواخت و قابل توسعه ایجاد می‌کند. تست Host تا پیش از مرحله ۴.۲، Publisher را با Test Double جایگزین می‌کند؛ اتصال واقعی `IPublishEndpoint` و Bus Outbox همچنان در مرحله ۴.۲ انجام می‌شود.

### D-048 — Controller، Validation خودکار و ApiResponse

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** API از Controller و Actionهای برگرداننده `IActionResult` استفاده می‌کند. مدل‌های ورودی Controller و Validatorهای آن‌ها در API و با پسوند `Request`، مدل‌های خروجی HTTP با پسوند `Response` و خروجی Use Case با پسوند `Result` نگهداری می‌شوند. ایجاد و Replay هر دو پاسخ یکسان `200 OK` در قالب `ApiResponse<CreateActivityResponse>` می‌دهند. `BaseController.HandleResult` تبدیل Result Pattern به `ApiResponse<T>` و Status Code مناسب را متمرکز می‌کند. Package FluentValidation و مکانیزم ثبت Assemblyهای Validator در Application قرار دارد و `AddApplication` خود Handler را نیز ثبت می‌کند. `AutoValidationFilter` سراسری Requestها را پیش از Action بررسی می‌کند؛ Handler دیگر `IValidator<CreateActivityCommand>` دریافت یا Validation را دستی اجرا نمی‌کند و Domain همچنان آخرین محافظ Invariantهاست. Headerها مستقیماً و بدون Attribute اختصاصی با `[FromHeader]` bind می‌شوند. تنظیم پاسخ خطای Model Binding در `AddPresentation` باقی می‌ماند تا خطاهای قبل از Action نیز برخلاف رفتار پیش‌فرض `[ApiController]` در قالب `ApiResponse<T>` باشند.
- **دلیل:** این ساختار از الگوی واقعی `NamadeNo.Investment` شامل `AutoValidationFilter`، `BaseController.HandleResult`، `ApiResponse<T>` و `GlobalExceptionMiddleware` اقتباس شده و abstractionهای اختصاصی غیرضروری حذف شده‌اند. تنظیم اضافه Model State آگاهانه حفظ شده است، زیرا NamadeNo در خطاهای JSON و Model Binding قالب ثابت `ApiResponse<T>` را تضمین نمی‌کند. در DidarCrm نیز `IsSuccess` برخلاف پیاده‌سازی مرجع از وجود یا نبود `Data` حدس زده نمی‌شود؛ نتیجه صریح `Result` مبنا قرار می‌گیرد.

### D-049 — زیرساخت محلی SQL Server و RabbitMQ

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** SQL Server 2022 و RabbitMQ Management با Docker Compose، Volume ماندگار، Health Check و Credential قابل جایگزینی با Environment Variable اجرا می‌شوند. یک اسکریپت Idempotent، Schema حداقلی نسخه قدیمی را پیش از Expand Migration در دیتابیس محلی می‌سازد.
- **دلیل:** Migration پروژه عمداً فرض می‌کند جداول قدیمی Production وجود دارند؛ ایجاد Baseline محلی خارج از Migration، رفتار واقعی Expand Migration را بدون تبدیل آن به Migration ساخت یک سیستم جدید حفظ می‌کند.

### D-050 — فعال‌سازی مرحله‌ای Bus Outbox

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** MassTransit RabbitMQ و Transactional Bus Outbox روی `CrmDbContext` فعال می‌شوند. `ActivityCreatedEvent` روی Exchange پایدار `didar-crm.activity-created` از نوع Fanout منتشر خواهد شد و نام Queueهای Notification و Webhook از اکنون ثابت است. Delivery Service تا زمان ایجاد Queue و Binding واقعی Consumerها غیرفعال می‌ماند؛ هر Queue نیز هم‌زمان با پیاده‌سازی همان Consumer ساخته می‌شود.
- **دلیل:** ساخت Receive Endpoint بدون Consumer می‌تواند پیام را بدون اثر تجاری مصرف کند و تحویل Event به Exchange پیش از ایجاد Queue پایدار نیز پیام را از دست می‌دهد. تا آماده‌شدن Consumerها، پیام‌ها باید در Outbox SQL باقی بمانند.

### D-051 — تنظیم Outbox Delivery در MassTransit 8.4

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** تنظیمات Delivery شامل `QueryMessageLimit`، `MessageDeliveryLimit`، `QueryDelay`، `QueryTimeout` و `MessageDeliveryTimeout` از Configuration خوانده می‌شوند. Flag فعال‌سازی نیز Configuration-driven است و تا ایجاد Queueهای Consumer خاموش باقی می‌ماند. Retry و Concurrency اختصاصی Delivery اضافه نمی‌شوند، زیرا API مستقیم آن‌ها در MassTransit 8.4 وجود ندارد و رفتار داخلی کتابخانه استفاده می‌شود.
- **دلیل:** استفاده از گزینه‌های نسخه‌های جدیدتر در نسخه 8.4 قابل اتکا نیست. همچنین فعال‌کردن Delivery پیش از وجود Queue پایدار می‌تواند پیام تحویل‌شده به Exchange را از دسترس Consumerهای آینده خارج کند.

### D-052 — Policy مشترک Consumerها

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** تمام Receive Endpointهایی که با `ConfigureEndpoints` ساخته می‌شوند، به‌صورت مشترک از Entity Framework Consumer Outbox روی `CrmDbContext`، Duplicate Detection Window سی‌دقیقه‌ای و سه Retry کوتاه با فاصله یک ثانیه استفاده می‌کنند. این مقادیر از Configuration خوانده می‌شوند. Redelivery زمان‌دار تا مشخص‌شدن خطاهای موقت واقعی هر Consumer و انتخاب مکانیزم سازگار با RabbitMQ به تعویق می‌افتد.
- **دلیل:** Consumer Outbox ثبت Inbox، تغییرات دیتابیس و پیام‌های خروجی Consumer را اتمیک می‌کند. Retry کوتاه برای خطاهای گذرا مناسب است، اما Redelivery بلندمدت یک تصمیم عملیاتی وابسته به Consumer است و نباید بدون نیاز واقعی یا Plugin لازم به همه Endpointها تحمیل شود.

### D-053 — اثبات بازیابی Bus Outbox پس از قطعی Broker

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** رفتار قطعی RabbitMQ با Integration Test واقعی روی SQL Server و RabbitMQ Testcontainer بررسی می‌شود. تست پس از برقراری اولیه Bus، Container RabbitMQ را Pause می‌کند، درخواست Activity را ثبت و ماندگاری پیام در Outbox را تأیید می‌کند؛ سپس Broker را Unpause کرده و تخلیه رکورد Outbox پس از Publisher Confirm را انتظار می‌کشد.
- **دلیل:** Mock یا In-memory Transport نمی‌تواند جدایی Commit دیتابیس از دسترس‌پذیری Broker و بازیابی Connection واقعی را اثبات کند. Pause/Unpause نسبت به Stop/Start کامل Container سریع‌تر و deterministic‌تر است و همان قطع دسترسی شبکه به Broker را شبیه‌سازی می‌کند.

### D-054 — مدل ماندگار Notification

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** اعلان ساخته‌شده در Entity دامنه `ActivityNotification` و جدول مستقل `crmNotification` ذخیره می‌شود. رکورد فقط شامل شناسه Notification، شناسه یکتای Activity و زمان ایجاد Notification است. وجود رکورد Notification وضعیت Completed را نشان می‌دهد و Unique Index روی `ActivityId` محافظ نهایی در برابر ایجاد تکراری است؛ Pending در Outbox/Queue و Failure در Error Queue نمایش داده می‌شود، بنابراین ستون Status تکراری به جدول اضافه نمی‌شود.
- **دلیل:** `ActivityId` به‌تنهایی Activity را به‌صورت یکتا مشخص می‌کند و برای ارتباط و idempotency کافی است. کپی Tenant، Contact، عنوان و زمان Activity در نیاز فعلی مصرفی ندارد و Snapshot غیرضروری ایجاد می‌کند. خود رکورد Notification مدرک اثر تجاری موفق است و Unique Constraint تضمین را در مرز دیتابیس نگه می‌دارد.

### D-055 — محل Consumerهای Messaging

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Consumerهای MassTransit به‌عنوان ورودی سیستم در پروژه API و کنار Controllerها قرار می‌گیرند. Consumer فقط پیام Transport را به Command لایه Application نگاشت می‌کند. تنظیم Broker، Outbox و Persistence در Infrastructure باقی می‌ماند و API ثبت Consumerها را از طریق callback به Registration زیرساخت اعلام می‌کند.
- **دلیل:** Consumer مانند Controller یک Inbound Adapter است. قرار دادن آن در API جهت وابستگی Clean Architecture را حفظ می‌کند و Infrastructure را از شناخت Host و Consumerهای Presentation بی‌نیاز نگه می‌دارد.

### D-056 — اتصال Notification Consumer به Inbox استاندارد MassTransit

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** برای Notification Consumer تنظیم اختصاصی و تکراری Outbox اضافه نمی‌شود. Callback سراسری Receive Endpointها که در مرحله ۴.۴ پیاده شده، `UseEntityFrameworkOutbox<CrmDbContext>` را روی Queue اعلان نیز اعمال می‌کند. در نتیجه پردازش پیام و ثبت Notification با `InboxState` و همان `CrmDbContext` در یک مرز تراکنشی قرار می‌گیرند و `DuplicateDetectionWindow` مشترک نیز روی آن اعمال می‌شود.
- **دلیل:** سیاست Consumer Outbox یک دغدغه زیرساختی مشترک بین Consumerها است. ثبت دوباره همان Middleware در `ActivityCreatedNotificationConsumerDefinition` هم تکراری است و هم احتمال رفتار مبهم در Pipeline را ایجاد می‌کند. مرحله ۵.۴ رفتار Deduplication را با تحویل مجدد واقعی اثبات خواهد کرد.

### D-057 — اثبات Deduplication در تحویل مجدد

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** رفتار Redelivery با انتشار دوباره یک `ActivityCreatedEvent` با Envelope `MessageId` یکسان روی RabbitMQ واقعی آزمایش می‌شود. تست یک رکورد `InboxState`، یک Notification و خالی‌شدن Queue پس از Ack هر دو Delivery را انتظار می‌کشد.
- **دلیل:** شمارش Notification به‌تنهایی می‌تواند پیش از رسیدن Delivery دوم false-positive شود. بررسی خالی‌شدن Queue از Management API اثبات می‌کند پیام تکراری واقعاً از Queue خارج شده و Inbox استاندارد MassTransit آن را بدون اجرای دوباره اثر تجاری مدیریت کرده است.

### D-058 — هویت و قرارداد Webhook Delivery

- **وضعیت:** اصلاح‌شده توسط D-060
- **تصمیم:** `WebhookDelivery` شناسه مستقل ندارد و `ActivityId` یکتا هویت آن است. مدل در این مرحله فقط `ActivityId`، وضعیت `Pending/Succeeded` و زمان ایجاد را نگه می‌دارد. همان `ActivityId` در Adapter HTTP آینده مبنای Header مربوط به Idempotency خواهد بود. Payload ارسال شامل اطلاعات Activity است، اما برای جلوگیری از Snapshot تکراری داخل Delivery ذخیره نمی‌شود. Port با نام `IWebhookSender` در Application تعریف می‌شود و موفقیت را با تکمیل `SendAsync` بیان می‌کند؛ طبقه‌بندی خطاها در مرحله Retry اضافه خواهد شد.
- **دلیل:** برای هر Activity فقط یک Webhook Delivery وجود دارد، پس شناسه دوم اطلاعات جدیدی ایجاد نمی‌کند. کلید مبتنی بر Activity حتی اگر پردازش پس از موفقیت HTTP و قبل از Commit قطع شود، در Redelivery ثابت می‌ماند و مقصد می‌تواند درخواست تکراری را تشخیص دهد.

### D-059 — مسیر موفق ارسال Webhook

- **وضعیت:** اصلاح‌شده توسط D-060
- **تصمیم:** `ActivityCreatedWebhookConsumer` در API از Queue پایدار Webhook مصرف می‌کند و اجرای Use Case را به `SendActivityWebhookHandler` می‌سپارد. Handler ابتدا Payload را از Event می‌سازد، آن را از طریق `IWebhookSender` ارسال می‌کند و فقط پس از پاسخ موفق، Delivery با وضعیت `Succeeded` را همراه Inbox در همان `CrmDbContext` ذخیره می‌کند. Adapter زیرساختی با `HttpClientFactory` درخواست POST می‌فرستد و `ActivityId` را در Header با نام `Idempotency-Key` قرار می‌دهد. جدول `crmWebhookDelivery` از `ActivityId` به‌عنوان Primary Key استفاده می‌کند.
- **دلیل:** Side Effect خارجی قابل Rollback نیست؛ اگر پس از موفقیت HTTP و پیش از Commit قطعی رخ دهد، پیام دوباره پردازش می‌شود. ثابت‌بودن Header به مقصد اجازه Deduplication می‌دهد و Primary Key مبتنی بر Activity نیز از ثبت دو نتیجه موفق جلوگیری می‌کند. رفتار خطا و Backoff در مرحله ۶.۳ تکمیل می‌شود.

### D-060 — جداسازی رزرو Delivery از Side Effect خارجی

- **وضعیت:** اصلاح‌شده توسط D-061
- **تصمیم:** `WebhookDelivery` شناسه مستقل `WebhookDeliveryId` دارد؛ `Id` کلید اصلی و `ActivityId` دارای Unique Index است. Consumer اول رویداد Activity را می‌گیرد و با یک Insert اتمیک مبتنی بر `UPDLOCK/HOLDLOCK` فقط یک Delivery با وضعیت `Pending` رزرو می‌کند. در همان Consumer Outbox پیام `WebhookDeliveryRequestedEvent` ثبت می‌شود. Consumer دوم Delivery را بارگذاری می‌کند، اگر قبلاً `Succeeded` باشد پایان می‌یابد، در غیر این صورت HTTP را با `WebhookDeliveryId` به‌عنوان `Idempotency-Key` ارسال و سپس وضعیت را موفق می‌کند.
- **دلیل:** Inbox فقط تکرار همان Envelope `MessageId` را تشخیص می‌دهد و رویداد منطقی تکراری با MessageId جدید می‌تواند از آن عبور کند. بررسی Unique Constraint بعد از HTTP دیر است، چون Side Effect خارجی Rollback نمی‌شود. رزرو اتمیک قبل از انتشار پیام ارسال باعث می‌شود فقط برنده Race به مرحله HTTP برسد. جداسازی دو Consumer نیز شناسه Delivery را پیش از Side Effect ماندگار می‌کند تا Retry پس از قطعی همیشه همان Idempotency Key را داشته باشد.

### D-061 — مسیر تک‌Consumer برای Webhook

- **وضعیت:** اصلاح‌شده توسط D-062
- **تصمیم:** ایجاد Delivery و ارسال HTTP در همان `ActivityCreatedWebhookConsumer` انجام می‌شوند. Handler ابتدا بر اساس `ActivityId` وجود Delivery را با Repository بررسی می‌کند؛ اگر وجود نداشت Entity با وضعیت `Pending` را از طریق EF اضافه و `SaveChangesAsync` اجرا می‌کند تا Unique Index پیش از HTTP بررسی شود. سپس درخواست با `ActivityId` به‌عنوان `Idempotency-Key` ارسال و وضعیت همان Entity به `Succeeded` تغییر می‌کند. Race نهایی با ترجمه خطای Unique Index به `DuplicateWebhookDeliveryException` مدیریت می‌شود و بازنده پیش از HTTP پایان می‌یابد.
- **دلیل:** برای این Use Case تنها یک عملیات Webhook به‌ازای هر Activity وجود دارد، بنابراین `ActivityId` کلید Idempotency پایدار عملیات است؛ حتی اگر تراکنش پس از HTTP Rollback شود، Retry همان Header را می‌فرستد. این انتخاب نیاز به Event و Consumer دوم را حذف می‌کند. `WebhookDeliveryId` همچنان هویت مستقل Entity و `ActivityId` فقط مرجع یکتا به Activity باقی می‌ماند.

### D-062 — ماندگاری Pending و خطای Webhook

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Webhook Endpoint از `UseEntityFrameworkOutbox<CrmDbContext>` سراسری مستثنا می‌شود؛ Notification و سایر Endpointها همچنان Consumer Outbox دارند. در Webhook Consumer، اولین `SaveChangesAsync` رکورد `Pending` را پیش از HTTP Commit می‌کند. در شکست HTTP، `LastError` و `LastAttemptAtUtc` ذخیره و Exception دوباره پرتاب می‌شود تا Retry کوتاه MassTransit اجرا شود. در موفقیت، Status به `Succeeded` تغییر، زمان آخرین تلاش ثبت و خطای قبلی پاک می‌شود.
- **دلیل:** با Consumer Outbox، تراکنش Middleware تا پایان Consumer باز می‌ماند و شکست HTTP ثبت Pending و خطا را Rollback می‌کند. این Endpoint خروجی Messaging ندارد و Idempotency آن با `ActivityId`، Unique Index و وضعیت Delivery تأمین می‌شود؛ بنابراین خارج‌کردن هدفمند آن از Consumer Outbox امکان مشاهده و بازیابی Pending و خطای آخر را بدون حذف Outbox از سایر Consumerها فراهم می‌کند.

### D-063 — Retry افزایشی Webhook و شمارنده تلاش

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Webhook Endpoint به‌جای Retry ثابت مشترک، از آرایه Configuration-driven با فاصله‌های افزایشی `۱، ۵ و ۱۵ ثانیه` استفاده می‌کند. هر پاسخ موفق یا ناموفق HTTP مقدار `AttemptCount` را یک واحد افزایش می‌دهد و همراه `LastAttemptAtUtc` ذخیره می‌شود. Retry سایر Consumerها بدون تغییر باقی می‌ماند.
- **دلیل:** فاصله افزایشی فشار روی مقصد موقتاً ناسالم را کاهش می‌دهد و شمارنده ماندگار امکان عیب‌یابی را فراهم می‌کند. در این مرحله از Retry درون‌حافظه‌ای کوتاه MassTransit استفاده می‌شود؛ Redelivery طولانی‌مدت به زیرساخت Scheduler/Delayed Delivery وابسته است و خارج از محدوده این زیرمرحله باقی می‌ماند. پس از اتمام تلاش‌ها، تصمیم وضعیت نهایی و Dead Letter در مرحله ۶.۴ انجام می‌شود.

### D-064 — شکست نهایی و بازیابی Webhook

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** شماره تلاش جاری از Retry Context خود MassTransit در Consumer خوانده می‌شود. شکست‌های قبل از آخرین Retry، Delivery را `Pending` نگه می‌دارند و شکست آخر آن را `Failed` می‌کند؛ سپس Exception همچنان پرتاب می‌شود تا MassTransit پیام را به Queue استاندارد `_error` منتقل کند. پردازش مجدد یک رویداد برای Delivery شکست‌خورده مجاز است و در صورت موفقیت همان رکورد را به `Succeeded` می‌رساند.
- **دلیل:** وضعیت دیتابیس پس از پایان Retry نباید به‌اشتباه یک پردازش فعال را نشان دهد. نگه‌داشتن پیام اصلی و جزئیات Exception در Error Queue امکان عیب‌یابی و Redrive عملیاتی را حفظ می‌کند و قابل‌بازیابی‌بودن Entity نیز نیاز به ساخت رکورد جدید یا حذف تاریخچه تلاش‌ها را از بین می‌برد.

### D-065 — نگاشت Flagهای قدیمی در Backfill

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** `NotificationCreated = 1` با ایجاد یک رکورد تاریخی در `crmNotification` به وضعیت جدید منتقل می‌شود؛ مقدار صفر تا مرحله Checkpoint/Resume بدون رکورد باقی می‌ماند. برای Webhook، مقدار `WebhookSent = 1` به Delivery با وضعیت `Succeeded` و مقدار صفر به Delivery با وضعیت `Pending` نگاشت می‌شود. شناسه جدول‌های جدید با `NEWID()` ساخته و `CreatedAt` از Activity کپی می‌شود. برای داده تاریخی، `AttemptCount = 0` و `LastAttemptAt` و `LastError` برابر `NULL` هستند.
- **دلیل:** Flag مثبت فقط وقوع نتیجه را ثابت می‌کند و زمان یا تعداد تلاش‌های قبلی را نشان نمی‌دهد؛ ساختن Telemetry حدسی اطلاعات نادرست تولید می‌کند. نبود رکورد Notification برای Flag منفی و وضعیت Pending برای Webhook منفی، موارد نیازمند پردازش را برای مرحله ۷.۴ قابل شناسایی نگه می‌دارند.

### D-066 — اجرای Batch و تکرارپذیری Backfill

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Backfill اعلان و Webhook در اسکریپت‌های مستقل، با Batch پیش‌فرض ۱۰۰۰ رکوردی، تراکنش جدا برای هر Batch و تأخیر قابل تنظیم اجرا می‌شوند. هر Insert با `NOT EXISTS` و Lock هدفمند روی Unique Index محافظت می‌شود و Unique Index مربوط به `ActivityId` تضمین نهایی عدم تکرار است.
- **دلیل:** تراکنش‌های کوتاه فشار Lock و Log را برای چند میلیون رکورد محدود می‌کنند. اسکریپت‌ها بدون حذف یا بازنویسی نتیجه قبلی قابل اجرای مجدد هستند و با Writeهای همزمان مسیر جدید تداخل داده‌ای ایجاد نمی‌کنند.

### D-067 — Release Ledger و Resume اتمیک

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Use Case با نام `ReleaseLegacyActivitiesHandler` در Application، Candidateها را از `ILegacyActivityBackfillRepository` دریافت می‌کند و برای هر Activity قدیمی ناقص یک `ActivityCreatedEvent` و یک Release ایجاد می‌کند. پیاده‌سازی EF Repository و مدل Persistence مربوط به جدول `crmActivityBackfillRelease` در Infrastructure باقی می‌مانند. Event و Release از طریق Bus Outbox و `IUnitOfWork` در یک `SaveChanges` ذخیره می‌شوند. Release Ledger شامل `ActivityId`، `MessageId` و `ReleasedAt` است و Activityهای دارای `RequestKey` را وارد Backfill نمی‌کند.
- **دلیل:** یک Cursor سراسری می‌تواند Write همزمان با شناسه کوچک‌تر را از دست بدهد و ثبت Checkpoint جدا از انتشار پیام Failure Gap می‌سازد. Ledger به‌ازای هر Activity هم Resume را مستقل از ترتیب شناسه امن می‌کند و هم همراه Outbox به‌صورت اتمیک Commit می‌شود.

### D-068 — Polling هنگام همزیستی و Reconciliation

- **وضعیت:** پذیرفته‌شده
- **تصمیم:** Worker پس از Batch خالی متوقف نمی‌شود و تا زمان غیرفعال‌سازی با Polling ادامه می‌دهد. Batch پر با تأخیر کوتاه و Batch ناقص/خالی با Polling Interval اجرا می‌شود. فعال‌سازی Worker مستلزم فعال‌بودن Outbox Delivery است. Reconciliation فقط از طریق اسکریپت عملیاتی مستقل انجام می‌شود؛ متد برنامه‌ای تکراری `ReconcileAsync` حذف شده است.
- **دلیل:** در دوره همزیستی، نسخه قدیمی ممکن است پس از عبور Backfill همچنان Activity بدون RequestKey بنویسد. Polling و Ledger آن Write را در دور بعد می‌گیرند؛ Queryهای Reconciliation نیز تفاوت «هنوز آزاد نشده» و «آزاد شده اما Consumer هنوز کامل نکرده» را قابل مشاهده می‌کنند.

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

- **وضعیت:** پذیرفته‌شده
- افزودن Package مربوط به MassTransit EF Core.
- افزودن Entityهای استاندارد `InboxState`، `OutboxMessage` و `OutboxState` به `CrmDbContext`.
- حذف مدل و Configuration اختصاصی Outbox.
- تکمیل تست EF Model؛ بدون اتصال RabbitMQ یا اجرای Delivery Service.

#### مرحله ۲.۳.۱ — Adapter انتشار Event

- **وضعیت:** پذیرفته‌شده
- پیاده‌سازی `IIntegrationEventPublisher` به‌عنوان Adapter روی `IPublishEndpoint`.
- ثبت Adapter در DI.
- فعال‌سازی Bus Outbox و اتصال RabbitMQ در مرحله ۴.۲ تکمیل می‌شود؛ تا آن مرحله `IPublishEndpoint` هنوز در Container ثبت نشده است.

#### مرحله ۲.۴.۱ — ActivityRepository

- **وضعیت:** پذیرفته‌شده
- پیاده‌سازی Query بر اساس Tenant و Request Key با `AsNoTracking`.
- پیاده‌سازی Add بدون Commit و ثبت Repository در DI.
- تست واقعی Query پس از ایجاد Migration و SQL Server Testcontainer اضافه می‌شود.

#### مرحله ۲.۴.۲ — ContactRepository

- **وضعیت:** پذیرفته‌شده
- پیاده‌سازی Repository فقط برای بازیابی Tracking شده‌ی Contact.
- فراخوانی رفتار Monotonic در `CreateActivityHandler`.
- ثبت Repository در DI و افزودن تست رفتار Domain.
- `LastActivityAt` به‌عنوان Concurrency Token ثبت شده است؛ Resolve و Retry در مرحله ۲.۵ و تست Relational در مرحله SQL Server انجام می‌شوند.

#### مرحله ۲.۵.۱ — Resolve مربوط به Contact Concurrency

- **وضعیت:** پذیرفته‌شده
- Resolve کردن Conflict فقط برای `Contact` و حفظ بزرگ‌ترین `LastActivityAt`.
- Retry محدود کل `SaveChangesAsync` برای حفظ Activity، Contact و Outbox در یک Commit.
- ترجمه عمومی خطای EF در `EfUnitOfWork` و Orchestration مربوط به Reload و Retry در Handler.
- اجرای مجدد رفتار Domain پس از Reload وضعیت دیتابیس.
- تست واقعی Retry پس از Migration روی SQL Server اضافه می‌شود.

#### مرحله ۲.۵.۲ — ترجمه Duplicate Request Key

- **وضعیت:** پذیرفته‌شده
- تبدیل خطای Unique Index مشخص `(TenantId, RequestKey)` به `DuplicateRequestKeyException`.
- سایر خطاهای `DbUpdateException` بدون تغییر منتشر می‌شوند.
- ایجاد خود Index و تست واقعی خطا در مرحله ۲.۶ انجام می‌شود.

#### مرحله ۲.۶.۱ — تعریف Filtered Unique Index

- **وضعیت:** پذیرفته‌شده
- تعریف Unique Index ترکیبی روی Tenant و Request Key با Filter مربوط به رکوردهای قدیمی.
- تأیید نام، ترتیب ستون‌ها، یکتایی و Filter در EF Model Test.
- هنوز Migration یا Index واقعی در SQL Server ایجاد نشده است.

#### مرحله ۲.۶.۲ — Expand Migration

- **وضعیت:** پذیرفته‌شده
- ایجاد Migration سازگار برای ستون `RequestKey`، جداول استاندارد MassTransit و Filtered Unique Index.
- حفظ جدول‌های قدیمی در هر دو مسیر `Up` و `Down` و افزودن Factory مستقل برای ابزار EF.

#### مرحله ۲.۶.۳ — تست Rollback تراکنش

- **وضعیت:** پذیرفته‌شده
- اثبات Rollback اتمیک Activity، Contact و Outbox روی SQL Server واقعی پس از اعمال Migration.
- اجرای تست روی SQL Server موقت با Testcontainers و Schema قدیمی واقعی.

#### مرحله ۲.۷ — تست Concurrency در SQL Server

- **وضعیت:** پذیرفته‌شده
- تست درخواست همزمان و Monotonic بودن `LastActivityAt` با Testcontainers.
- اجبار ترتیب Save برای اثبات اجرای Conflict و Retry، نه صرفاً نتیجه‌ای وابسته به زمان‌بندی Threadها.

### مرحله ۳ — HTTP API

#### مرحله ۳.۱ — Endpoint و DTOها

- **وضعیت:** پذیرفته‌شده
- افزودن `POST /api/v1/activities` با Controller و Action دارای خروجی `IActionResult`.
- افزودن Request/Response مستقل HTTP، ثبت Handler و پیاده‌سازی Production مربوط به `IClock`.

#### مرحله ۳.۲ — Validation و Problem Details

- **وضعیت:** پذیرفته‌شده
- اعتبارسنجی خودکار Body Request با FluentValidation و Headerها با Model Binding؛ بدون تزریق Validator در Handler.
- نگاشت مشترک Resultهای Application به `ApiResponse<T>` توسط `BaseController.HandleResult`.

#### مرحله ۳.۳ — پاسخ Replay و تست Endpoint

- **وضعیت:** پذیرفته‌شده
- پاسخ یکسان `200 OK` برای ایجاد و Replay بدون نمایش نوع نتیجه به Client.
- افزودن `[ProducesResponseType]`، Swagger و Integration Testهای HTTP و OpenAPI.
- تست Host واقعی با `WebApplicationFactory` و SQL Server موقت برای Success، Replay، Validation فارسی، Not Found و Conflict.

### مرحله ۴ — RabbitMQ و MassTransit

#### مرحله ۴.۱ — Docker Compose زیرساخت محلی

- **وضعیت:** پذیرفته‌شده
- افزودن SQL Server و RabbitMQ و مستند اجرای محلی.

#### مرحله ۴.۲ — اتصال و توپولوژی RabbitMQ

- **وضعیت:** پذیرفته‌شده
- افزودن Transport مربوط به MassTransit RabbitMQ، Exchange نوع Fanout و نام‌های ثابت Queueها.
- فعال‌سازی Transactional Bus Outbox روی `CrmDbContext`، تکمیل Dependency مربوط به `IPublishEndpoint` و غیرفعال‌ماندن موقت Delivery Service تا مرحله ۴.۳.
- ایجاد Receive Endpoint و Binding واقعی Queueها همراه Consumerهای مراحل ۵ و ۶ انجام می‌شود.

#### مرحله ۴.۳ — تنظیم Outbox Delivery

- **وضعیت:** پذیرفته‌شده
- تنظیم Query/Delivery Batch Size، Query Delay، Query Timeout و Delivery Timeout مربوط به Bus Outbox.
- آماده‌سازی Flag فعال‌سازی Delivery و خاموش نگه‌داشتن آن تا ایجاد Queueهای پایدار Consumer.
- استفاده از رفتار داخلی Retry و Concurrency نسخه 8.4 به‌جای تنظیم APIهای نسخه‌های جدیدتر.

#### مرحله ۴.۴ — Consumer Outbox و Retry

- **وضعیت:** پذیرفته‌شده
- تنظیم Consumer Outbox و Duplicate Detection Window برای تمام Receive Endpointهای آینده.
- تنظیم Retry کوتاه مشترک و Configuration-driven.
- به‌تعویق‌انداختن Redelivery بلندمدت تا پیاده‌سازی Consumer و شناخت Failure Mode واقعی آن.

#### مرحله ۴.۵ — تست قطع Broker

- **وضعیت:** پذیرفته‌شده
- اثبات ثبت Activity و پیام Outbox هنگام قطعی واقعی RabbitMQ.
- اثبات تخلیه Outbox و انتشار پیام پس از بازیابی Broker.

### مرحله ۵ — Notification Consumer

#### مرحله ۵.۱ — مدل و Schema اعلان

- **وضعیت:** پذیرفته‌شده
- افزودن Entity و جدول مستقل `crmNotification` خارج از `crmActivity`.
- ثبت داده اعلان موفق و Unique Index روی `ActivityId` به‌عنوان محافظ نهایی تکرار.

#### مرحله ۵.۲ — Consumer مسیر موفق

- **وضعیت:** پذیرفته‌شده
- مصرف `ActivityCreatedEvent` از Queue پایدار Notification.
- نگاشت پیام به Application Handler و ایجاد اتمیک Notification با Consumer Outbox.

#### مرحله ۵.۳ — Inbox و Deduplication

- **وضعیت:** پذیرفته‌شده
- اتصال Consumer به Consumer Outbox و `InboxState` استاندارد MassTransit از طریق Callback سراسری مرحله ۴.۴.
- اعمال `DuplicateDetectionWindow` مشترک بدون ثبت دوباره Middleware در Consumer Definition.

#### مرحله ۵.۴ — تست Redelivery

- **وضعیت:** پذیرفته‌شده
- انتشار دو Delivery با Envelope `MessageId` یکسان روی RabbitMQ واقعی.
- اثبات ایجاد تنها یک Inbox و یک Notification پس از مصرف و Ack هر دو Delivery.

### مرحله ۶ — Webhook Delivery

#### مرحله ۶.۱ — مدل Delivery و HTTP Port

- **وضعیت:** پذیرفته‌شده
- افزودن مدل `WebhookDelivery` با `WebhookDeliveryId` مستقل، `ActivityId` یکتا و وضعیت اولیه `Pending`.
- افزودن رفتار موفقیت و قرارداد `IWebhookSender` همراه Payload مستقل.
- نگاشت EF، Consumer و Adapter واقعی HTTP در این مرحله پیاده‌سازی نمی‌شوند.

#### مرحله ۶.۲ — ارسال موفق Webhook

- **وضعیت:** پذیرفته‌شده
- افزودن Schema و Repository مربوط به `crmWebhookDelivery`.
- بررسی و ثبت `Pending` با EF در همان Consumer، پیش از Side Effect خارجی.
- ارسال POST با `Idempotency-Key` مبتنی بر `ActivityId` و سپس ثبت وضعیت `Succeeded`.
- مدیریت Race مربوط به Unique ActivityId پیش از ارسال HTTP.
- افزودن تست Handler، قرارداد HTTP، EF Model و Migration.

#### مرحله ۶.۳ — Retry و Backoff

- **وضعیت:** پذیرفته‌شده
- ثبت ماندگار آخرین خطا، زمان آخرین تلاش و `AttemptCount`.
- Retry مخصوص Webhook با Backoff افزایشی و Configuration-driven.
- حفظ Retry ثابت مشترک برای سایر Consumerها.

#### مرحله ۶.۴ — خطای دائمی و Dead Letter

- **وضعیت:** پذیرفته‌شده
- تشخیص آخرین تلاش از Retry Context و ثبت وضعیت نهایی `Failed`.
- انتقال پیام شکست‌خورده به Error Queue استاندارد MassTransit با پرتاب مجدد خطا.
- امکان پردازش مجدد Delivery شکست‌خورده و تست تبدیل آن به `Succeeded`.

### مرحله ۷ — Migration و Backfill

#### مرحله ۷.۱ — طراحی Schema انتقال

- **وضعیت:** پذیرفته‌شده
- ثبت نگاشت صریح Flagهای قدیمی به جدول‌ها و وضعیت‌های جدید.
- حفظ موارد ناقص برای پردازش کنترل‌شده مرحله ۷.۴ و پرهیز از ساخت Telemetry حدسی.

#### مرحله ۷.۲ — Backfill اعلان

- **وضعیت:** پذیرفته‌شده
- اسکریپت Batch و قابل اجرای مجدد برای انتقال اعلان‌های تکمیل‌شده تاریخی.
- تراکنش مستقل هر Batch، تأخیر قابل تنظیم و محافظت نهایی با Unique Index.
- تست روی SQL Server واقعی برای نگاشت Flag و اجرای مجدد.

#### مرحله ۷.۳ — Backfill وب‌هوک

- **وضعیت:** پذیرفته‌شده
- اسکریپت Batch و قابل اجرای مجدد برای ساخت Deliveryهای `Succeeded` و `Pending`.
- حفظ ناشناخته‌بودن Telemetry تاریخی با `AttemptCount = 0` و مقادیر آخرین تلاش برابر `NULL`.
- تست روی SQL Server واقعی برای نگاشت هر دو مقدار Flag و اجرای مجدد.

#### مرحله ۷.۴ — Checkpoint و Resume

- **وضعیت:** آماده‌بازبینی
- افزودن Release Ledger یکتا به‌ازای Activity و Migration مربوط به آن.
- Orchestration در `ReleaseLegacyActivitiesHandler` لایه Application و دسترسی EF پشت Repository لایه Infrastructure.
- ثبت اتمیک Release و `ActivityCreatedEvent` در Bus Outbox با `IUnitOfWork`.
- Worker اختیاری با Batch و تأخیر Configuration-driven و تست Resume بدون انتشار تکراری.

#### مرحله ۷.۵ — Concurrent Write و Reconciliation

- **وضعیت:** آماده‌بازبینی
- ادامه Polling پس از Batch خالی برای پوشش Writeهای همزمان نسخه قدیمی.
- محدودکردن Backfill به رکوردهای Legacy با `RequestKey IS NULL`.
- افزودن اسکریپت مستقل Reconciliation برای تفکیک موارد آزاد‌نشده و Side Effectهای در انتظار؛ بدون API برنامه‌ای تکراری.
- تست درج Legacy پس از Poll خالی و ردکردن Activity مسیر جدید.

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
| ۱۴۰۵/۰۸/۲۹ | زیرمرحله ۲.۳.۱ شامل Adapter قرارداد انتشار Application روی `IPublishEndpoint` با حفظ Runtime Type پیام پیاده‌سازی شد؛ تصمیم D-036 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | زیرمرحله ۲.۴.۱ شامل پیاده‌سازی و ثبت `ActivityRepository` با Query بدون Tracking و Add بدون Commit انجام شد؛ تصمیم D-037 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | زیرمرحله ۲.۴.۲ شامل رفتار Monotonic در `Contact`، پیاده‌سازی `ContactRepository` و ثبت Concurrency Token انجام شد؛ تصمیم D-038 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | پس از بازبینی مرحله ۲.۴.۲، فراخوانی `AdvanceLastActivityAt` از Repository به Handler منتقل و مسئولیت Repository به بازیابی Contact محدود شد. |
| ۱۴۰۵/۰۸/۲۹ | زیرمرحله ۲.۵.۱ شامل Resolve هدفمند Contact Concurrency و Retry محدود `SaveChangesAsync` پیاده‌سازی شد؛ تصمیم D-039 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | پس از بازبینی مرحله ۲.۵.۱، `EfUnitOfWork` از `CrmDbContext` جدا و مقایسه‌ی تجاری Timestamp از Infrastructure حذف شد؛ تصمیم D-040 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | پس از بازبینی دوباره مرحله ۲.۵.۱، شناخت Contact و Retry از `EfUnitOfWork` حذف و به Orchestration لایه Application منتقل شد؛ تصمیم D-041 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | زیرمرحله ۲.۵.۲ شامل ترجمه هدفمند خطاهای 2601/2627 مربوط به Index درخواست به `DuplicateRequestKeyException` پیاده‌سازی شد؛ تصمیم D-042 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | زیرمرحله ۲.۶.۱ شامل تعریف و تست EF Model مربوط به Filtered Unique Index درخواست پیاده‌سازی شد؛ تصمیم D-043 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | زیرمرحله ۲.۶.۲ شامل Migration افزایشی ستون Request Key، جداول MassTransit و Index درخواست پیاده‌سازی شد؛ تصمیم D-044 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | زیرمرحله ۲.۶.۳ با تست Rollback اتمیک Contact، Activity و Outbox روی SQL Server واقعی پیاده‌سازی شد؛ تصمیم D-045 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | مرحله ۲.۷ با تست قطعی Contact Concurrency، Reload و Retry روی دو DbContext واقعی پیاده‌سازی شد؛ تصمیم D-046 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | مراحل ۳.۱ تا ۳.۳ شامل Endpoint ثبت Activity، Validation، Problem Details، پاسخ Created/Replay و تست‌های HTTP پیاده‌سازی شدند؛ تصمیم D-047 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | بنا بر بازبینی API، Outcome و Problem Details با Controller، Result خروجی Application، Validation خودکار، `HandleResult`، `ApiResponse<T>` و Swagger جایگزین شدند؛ تصمیم D-048 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | الگوی API با ماژول `NamadeNo.Investment` تطبیق داده شد؛ Request Validatorها در API باقی ماندند، ثبت Validation مربوط به Use Case به Application منتقل و abstractionهای اختصاصی Header و Error Factory حذف شدند. |
| ۱۴۰۵/۰۸/۲۹ | وضعیت مراحل بازبینی‌شده ۲.۵.۲ تا ۳.۳ به «پذیرفته‌شده» تغییر کرد. |
| ۱۴۰۵/۰۸/۲۹ | مراحل ۴.۱ و ۴.۲ شامل Docker Compose، Baseline محلی جداول قدیمی، اتصال RabbitMQ، Topic Exchange و Bus Outbox بدون Delivery Service پیاده‌سازی شدند؛ تصمیم‌های D-049 و D-050 ثبت شدند. |
| ۱۴۰۵/۰۸/۲۹ | نوع Exchange مربوط به `ActivityCreatedEvent` به‌دلیل نیاز همه Consumerها به تمام پیام‌ها و نبود Routing Key از Topic به Fanout تغییر کرد؛ تصمیم‌های D-012 و D-050 اصلاح شدند. |
| ۱۴۰۵/۰۸/۲۹ | مرحله ۴.۳ شامل تنظیم Configuration-driven مربوط به Query و Delivery در Bus Outbox پیاده‌سازی شد؛ فعال‌سازی Delivery تا ایجاد Queueهای Consumer به تعویق افتاد و تصمیم D-051 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | مرحله ۴.۴ شامل Consumer Outbox، بازه Duplicate Detection و Retry کوتاه مشترک برای Endpointهای آینده پیاده‌سازی شد؛ تصمیم D-052 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | مرحله ۴.۵ با Integration Test واقعی SQL Server و RabbitMQ برای ثبت هنگام قطعی و انتشار پس از بازیابی Broker پیاده‌سازی شد؛ تصمیم D-053 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | مراحل ۵.۱ و ۵.۲ شامل Entity و Schema مستقل Notification، Repository و Handler، Consumer ورودی در API و تست end-to-end روی RabbitMQ پیاده‌سازی شدند؛ تصمیم‌های D-054 و D-055 ثبت شدند. |
| ۱۴۰۵/۰۸/۲۹ | مرحله ۵.۳ با اتصال خودکار Notification Consumer به Consumer Outbox و `InboxState` از طریق سیاست سراسری مرحله ۴.۴ تکمیل شد؛ از ثبت تکراری Middleware جلوگیری و تصمیم D-056 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | مرحله ۵.۴ با تست Redelivery واقعی روی RabbitMQ، بررسی یک Notification و یک Inbox و انتظار برای تخلیه Queue تکمیل شد؛ تصمیم D-057 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | مرحله ۶.۱ شامل مدل مینیمال Webhook Delivery با هویت Activity، وضعیت Pending/Succeeded و Port ارسال HTTP پیاده‌سازی شد؛ تصمیم D-058 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | مرحله ۶.۲ شامل Queue و Consumer وب‌هوک، Handler مسیر موفق، Adapter مبتنی بر HttpClientFactory، Header پایدار Idempotency و Schema مستقل Delivery پیاده‌سازی شد؛ تصمیم D-059 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | پس از بازبینی مرحله ۶.۲، WebhookDeliveryId مستقل اضافه و رزرو یکتای Delivery از ارسال HTTP جدا شد؛ جریان به دو Consumer و پیام میانی Outbox تقسیم و تصمیم D-060 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | پس از بازبینی مجدد مرحله ۶.۲، Event و Consumer میانی حذف و جریان به یک Consumer بازگردانده شد؛ ثبت Pending پیش از HTTP و مدیریت خطای Unique مطابق D-061 جایگزین شدند. |
| ۱۴۰۵/۰۸/۲۹ | Webhook Endpoint از Consumer Outbox سراسری مستثنا شد تا Pending و آخرین خطای HTTP پیش از Retry ماندگار شوند؛ تصمیم D-062 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | مرحله ۶.۳ با AttemptCount ماندگار و Retry افزایشی مخصوص Webhook تکمیل شد؛ تصمیم D-063 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | مرحله ۶.۴ با وضعیت نهایی Failed، انتقال پیام به Error Queue و امکان بازیابی Delivery شکست‌خورده تکمیل شد؛ تصمیم D-064 ثبت شد. |
| ۱۴۰۵/۰۸/۲۹ | وضعیت مراحل ۴.۱ تا ۶.۴ که بازبینی و مبنای مراحل بعدی قرار گرفته بودند، از «آماده‌بازبینی» به «پذیرفته‌شده» اصلاح شد. |
| ۱۴۰۵/۰۸/۲۹ | مراحل ۷.۱ تا ۷.۳ شامل نگاشت Flagهای قدیمی، Backfill دسته‌ای اعلان و Webhook و تست اجرای مجدد روی SQL Server واقعی پیاده‌سازی شدند؛ تصمیم‌های D-065 و D-066 ثبت شدند. |
| ۱۴۰۵/۰۸/۲۹ | مراحل ۷.۱ تا ۷.۳ پس از ادامه به Release ناقص پذیرفته شدند؛ مراحل ۷.۴ و ۷.۵ با Release Ledger اتمیک، Worker قابل Resume، پوشش Write همزمان و Reconciliation پیاده‌سازی و تصمیم‌های D-067 و D-068 ثبت شدند. |
| ۱۴۰۵/۰۸/۲۹ | بنا بر بازبینی Clean Architecture، Orchestration مربوط به Release از Infrastructure به Handler لایه Application منتقل، EF پشت Repository هدفمند قرار گرفت و `ReconcileAsync` تکراری حذف شد. |

</div>
