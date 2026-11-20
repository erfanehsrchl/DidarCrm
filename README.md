# Didar CRM Activity Redesign

This repository implements the incremental redesign described in the backend assignment. The solution must:

- insert a new Activity and update the existing Contact's `LastActivityAt` value in one database transaction;
- prevent duplicate Activity records when the same request is retried; and
- process Notifications and Webhooks asynchronously, without keeping the API request open.

The project uses .NET 8, ASP.NET Core, SQL Server, Entity Framework Core, MassTransit, and RabbitMQ. It is organized as a simple monolith using Clean Architecture. See [ARCHITECTURE.md](ARCHITECTURE.md) for the main design decisions.

## Prerequisites

- .NET 8 SDK
- Docker Desktop using Linux containers
- `dotnet-ef` version 8

Install the EF Core CLI if it is not already available:

```powershell
dotnet tool install --global dotnet-ef --version 8.*
```

## Local setup

Start SQL Server and RabbitMQ from the repository root:

```powershell
docker compose up -d
docker compose ps
```

The local services use the following ports:

| Service | Address |
|---|---|
| SQL Server | `localhost:1433` |
| RabbitMQ | `localhost:5672` |
| RabbitMQ Management UI | `http://localhost:15672` |

The credentials committed to this repository are intended for local development only. Production credentials must be supplied through a secret store or environment variables.

The migrations assume that the legacy `crmActivity` and `cntContact` tables already exist. For local development, create the minimal legacy schema before applying the EF Core migrations:

```powershell
docker compose exec -T sql-server bash -lc '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -i /scripts/local/create-legacy-schema.sql'

dotnet ef database update `
  --project src/DidarCrm.Infrastructure `
  --startup-project src/DidarCrm.Api
```

Outbox delivery is disabled in the default configuration. Enable it before starting the API if messages should be delivered to RabbitMQ:

```powershell
$env:OutboxDelivery__Enabled = "true"
dotnet run --project src/DidarCrm.Api
```

Swagger is available at `http://localhost:5269/swagger`.

## Creating an Activity

The Contact must already exist in `cntContact` for the specified Tenant.

```http
POST /api/v1/activities
X-Tenant-Id: 719e96ef-a651-43e7-bdcf-c755d8de8598
Idempotency-Key: activity-request-1001
Content-Type: application/json

{
  "contactId": "1b4eb018-e9d6-4d03-bd06-735d18243bd2",
  "title": "Follow up"
}
```

Repeating the same `Idempotency-Key` for the same Tenant and payload returns the original `ActivityId`. Reusing the key with a different payload returns a conflict response.

The most relevant settings can be overridden through environment variables:

- `ConnectionStrings__Database`
- `RabbitMq__Host`, `RabbitMq__Username`, and `RabbitMq__Password`
- `Webhook__Endpoint`
- `OutboxDelivery__Enabled`
- `LegacyActivityBackfill__Enabled`

## Running the tests

```powershell
dotnet build DidarCrm.sln
dotnet test DidarCrm.sln --no-build
```

The integration tests use Testcontainers, so Docker must be running. The test suite covers concurrent and repeated requests, duplicate message delivery, broker recovery, transaction rollback, and repeatable Backfill execution.

## Migration and Backfill order

Use the following sequence against an existing production database:

1. Back up the database and apply the Expand migrations. These changes remain compatible with the legacy writer.
2. Deploy the new version so that its durable queues are created, then enable Outbox delivery.
3. Run `scripts/backfill/01-backfill-notifications.sql` and `02-backfill-webhook-deliveries.sql`.
4. Enable both `LegacyActivityBackfill__Enabled` and `OutboxDelivery__Enabled`. The worker releases incomplete legacy records and continues to detect writes made by the legacy version during the transition.
5. Run `scripts/backfill/03-reconcile-backfill.sql` and investigate any remaining differences.
6. After the legacy writer has been stopped and reconciliation reports no remaining differences, disable the worker.

Removing the legacy columns must be handled by a separate Contract migration after the transition is complete. This repository intentionally does not include that migration so that rollback to the legacy version remains possible.

The Backfill scripts commit small batches and are safe to rerun. See the [Backfill runbook](scripts/backfill/README.md) for the exact mapping and recovery behavior.

## Failure and recovery scenarios

- **RabbitMQ outage:** Start the API, stop RabbitMQ, and create an Activity. The database transaction should still commit and the message should remain in the Outbox. MassTransit delivers it after the broker connection is restored.
- **Webhook outage:** The Consumer retries after 1, 5, and 15 seconds. If every attempt fails, the `WebhookDelivery` is marked as `Failed` and the message is moved to the MassTransit error queue. After fixing the destination, the message can be redriven or republished in a controlled operation.
- **Backfill rerun:** Running the scripts again must not create duplicate records. Unique indexes and the release ledger provide the final safeguards. Use the reconciliation script to verify the result.

## Scope and assumptions

- Authentication and the full Contact model are outside the assignment scope.
- The API atomically inserts the Activity, updates `Contact.LastActivityAt`, and writes the Outbox message.
- Notification and Webhook processing happens asynchronously and outside the API transaction.
- The solution is a simple monolith. Notification and Webhook processing have separate asynchronous boundaries but are not deployed independently.
