# Legacy Activity Backfill Runbook

These scripts migrate existing processing state from the legacy flags in `crmActivity` to the new Notification and Webhook tables. Apply the EF Core migrations before running any Backfill script.

## Data mapping

| Legacy value | New model |
|---|---|
| `NotificationCreated = 1` | Insert a historical record into `crmNotification`. |
| `NotificationCreated = 0` | Insert nothing. The Activity remains eligible for the checkpoint/resume worker. |
| `WebhookSent = 1` | Insert a `WebhookDelivery` with status `Succeeded`. |
| `WebhookSent = 0` | Insert a `WebhookDelivery` with status `Pending`. |

Historical records copy `CreatedAt` from the Activity. The legacy schema does not contain the actual attempt count, last-attempt time, or error, so `AttemptCount` is initialized to zero while `LastAttemptAt` and `LastError` remain `NULL`.

## Execution order

1. Apply the EF Core migrations to create the new schema.
2. Run `01-backfill-notifications.sql`.
3. Run `02-backfill-webhook-deliveries.sql`.
4. Enable both `OutboxDelivery.Enabled` and `LegacyActivityBackfill.Enabled`.
5. Run `03-reconcile-backfill.sql` and investigate every non-zero result.
6. Stop the worker only after the legacy writer has stopped and reconciliation is clean.

Each script commits small batches and is safe to rerun. A unique index on `ActivityId` is the final database-level safeguard against duplicate records. Batch size and the delay between batches can be configured at the beginning of each script.

## Releasing incomplete legacy records

The worker selects only legacy Activities where `RequestKey IS NULL`. If the corresponding Notification does not exist or the Webhook has not reached `Succeeded`, it publishes an `ActivityCreatedEvent` and writes a unique row to `crmActivityBackfillRelease`.

The Bus Outbox message and release-ledger row are saved by the same `SaveChanges` call. Stopping the process between batches therefore does not lose a released event.

The worker continues polling after an empty batch so that it can detect writes made by the legacy version during the transition. Activities created through the new path have a `RequestKey` and are excluded. The reconciliation script reports three categories separately: differences between successful legacy flags and the new state, incomplete records that were never released, and released side effects that are still pending.
