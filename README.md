<div dir="rtl" align="right">

# پروژه بازطراحی Activity در Didar CRM

این Repository یک تمرین بازطراحی تدریجی با استفاده از .NET 8، SQL Server، RabbitMQ و Clean Architecture است.

مرجع زنده محدوده، تصمیم‌های معماری و مراحل پیاده‌سازی در فایل [`PROJECT_PLAN.md`](PROJECT_PLAN.md) نگهداری می‌شود.

## وضعیت فعلی

Endpoint ثبت Activity، SQL Persistence، Transactional Bus Outbox و اتصال RabbitMQ پیاده‌سازی شده‌اند. Outbox Delivery تا مرحله ۴.۳ عمداً غیرفعال است تا پیش از ایجاد Queueهای Consumer پیامی به Exchange بدون Subscriber تحویل داده نشود.

## اجرای زیرساخت محلی

از ریشه Repository اجرا کنید:

<div dir="ltr" align="left">

```powershell
docker compose up -d
docker compose ps
```

</div>

پورت SQL Server برابر `1433`، پورت AMQP برابر `5672` و پنل مدیریت RabbitMQ در `http://localhost:15672` است. نام کاربری RabbitMQ برابر `didarcrm` و رمز پیش‌فرض محیط محلی برابر `DidarCrm!Local123` است.

چون Migration پروژه از یک دیتابیس قدیمی موجود شروع می‌شود، در اجرای محلی ابتدا Schema حداقلی نسخه قدیمی را ایجاد و سپس Migration را اعمال کنید:

<div dir="ltr" align="left">

```powershell
docker compose exec -T sql-server bash -lc '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -i /scripts/local/create-legacy-schema.sql'

dotnet ef database update `
  --project src/DidarCrm.Infrastructure `
  --startup-project src/DidarCrm.Api

dotnet run --project src/DidarCrm.Api
```

</div>

مقادیر Connection و Credentialهای محلی از طریق `appsettings.Development.json` و بخش `RabbitMq` خوانده می‌شوند و در محیط‌های دیگر باید با Secret یا Environment Variable جایگزین شوند؛ برای نمونه `RabbitMq__Password` و `ConnectionStrings__Database`.

</div>
