<div dir="rtl" align="right">

# راهنمای Backfill وضعیت‌های قدیمی Activity

این پوشه فقط انتقال داده‌های موجود از Flagهای قدیمی `crmActivity` به جدول‌های جدید را انجام می‌دهد. Schema باید پیش از اجرای این اسکریپت‌ها با EF Migration ایجاد شده باشد.

## نگاشت داده‌ها

| مقدار قدیمی | نتیجه در مدل جدید |
|---|---|
| `NotificationCreated = 1` | یک رکورد تاریخی در `crmNotification` ایجاد می‌شود. |
| `NotificationCreated = 0` | رکوردی ایجاد نمی‌شود و Activity برای پردازش مرحله Checkpoint/Resume باقی می‌ماند. |
| `WebhookSent = 1` | یک `WebhookDelivery` با وضعیت `Succeeded` ایجاد می‌شود. |
| `WebhookSent = 0` | یک `WebhookDelivery` با وضعیت `Pending` ایجاد می‌شود. |

برای رکوردهای تاریخی، مقدار `CreatedAt` از Activity کپی می‌شود. چون زمان و تعداد واقعی تلاش‌های قدیمی مشخص نیست، `AttemptCount` برابر صفر و `LastAttemptAt` و `LastError` برابر `NULL` قرار می‌گیرند.

## ترتیب اجرا

1. اعمال EF Migrationها و ایجاد جدول‌های جدید.
2. اجرای `01-backfill-notifications.sql`.
3. اجرای `02-backfill-webhook-deliveries.sql`.
4. فعال‌کردن همزمان `OutboxDelivery.Enabled` و `LegacyActivityBackfill.Enabled`.
5. اجرای `03-reconcile-backfill.sql` و بررسی صفرشدن موارد آزاد‌نشده.
6. پس از توقف کامل Writer قدیمی و پایان Reconciliation، غیرفعال‌کردن Worker.

هر اسکریپت در Batchهای کوتاه Commit می‌کند و اجرای دوباره آن امن است. `Unique Index` روی `ActivityId` محافظ نهایی در برابر ایجاد رکورد تکراری است. اندازه Batch و تأخیر میان Batchها در ابتدای هر فایل قابل تنظیم هستند.

## آزادسازی موارد ناقص

Worker فقط Activityهای قدیمی با `RequestKey IS NULL` را بررسی می‌کند. اگر Notification متناظر وجود نداشته باشد یا Webhook به وضعیت `Succeeded` نرسیده باشد، یک `ActivityCreatedEvent` منتشر می‌کند و همزمان یک رکورد یکتا در `crmActivityBackfillRelease` می‌نویسد. پیام Bus Outbox و Release Ledger در یک `SaveChanges` ذخیره می‌شوند؛ بنابراین توقف برنامه بین Batchها باعث گم‌شدن پیام نمی‌شود.

Worker پس از یک Batch خالی نیز با `PollingInterval` ادامه می‌دهد تا Writeهای همزمان نسخه قدیمی را ببیند. Activityهای مسیر جدید که `RequestKey` دارند وارد Backfill نمی‌شوند. فایل Reconciliation اختلاف Flagهای مثبت قدیمی با وضعیت جدید، موارد ناقص آزاد‌نشده و Side Effectهای در انتظار Consumer را جداگانه گزارش می‌کند.

</div>
