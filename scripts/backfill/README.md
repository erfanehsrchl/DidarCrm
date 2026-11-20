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
4. اجرای Checkpoint/Resume مرحله ۷.۴ برای موارد ناقص قدیمی.

هر اسکریپت در Batchهای کوتاه Commit می‌کند و اجرای دوباره آن امن است. `Unique Index` روی `ActivityId` محافظ نهایی در برابر ایجاد رکورد تکراری است. اندازه Batch و تأخیر میان Batchها در ابتدای هر فایل قابل تنظیم هستند.

</div>
