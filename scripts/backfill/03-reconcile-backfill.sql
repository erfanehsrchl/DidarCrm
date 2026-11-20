SET NOCOUNT ON;

SELECT
    COUNT_BIG(*) AS [CompletedNotificationFlagWithoutNotification]
FROM [dbo].[crmActivity] AS activity
WHERE activity.[NotificationCreated] = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM [dbo].[crmNotification] AS notification
      WHERE notification.[ActivityId] = activity.[Id]
  );

SELECT
    COUNT_BIG(*) AS [CompletedWebhookFlagWithoutSucceededDelivery]
FROM [dbo].[crmActivity] AS activity
WHERE activity.[WebhookSent] = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM [dbo].[crmWebhookDelivery] AS delivery
      WHERE delivery.[ActivityId] = activity.[Id]
        AND delivery.[Status] = N'Succeeded'
  );

SELECT
    COUNT_BIG(*) AS [IncompleteLegacyActivitiesNotReleased]
FROM [dbo].[crmActivity] AS activity
WHERE activity.[RequestKey] IS NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM [dbo].[crmActivityBackfillRelease] AS release
      WHERE release.[ActivityId] = activity.[Id]
  )
  AND
  (
      NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[crmNotification] AS notification
          WHERE notification.[ActivityId] = activity.[Id]
      )
      OR NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[crmWebhookDelivery] AS delivery
          WHERE delivery.[ActivityId] = activity.[Id]
            AND delivery.[Status] = N'Succeeded'
      )
  );

SELECT
    COALESCE(SUM(CASE WHEN notification.[ActivityId] IS NULL THEN 1 ELSE 0 END), 0)
        AS [ReleasedAwaitingNotification],
    COALESCE(SUM(CASE WHEN delivery.[Status] IS NULL OR delivery.[Status] <> N'Succeeded'
        THEN 1 ELSE 0 END), 0) AS [ReleasedAwaitingWebhook]
FROM [dbo].[crmActivityBackfillRelease] AS release
LEFT JOIN [dbo].[crmNotification] AS notification
    ON notification.[ActivityId] = release.[ActivityId]
LEFT JOIN [dbo].[crmWebhookDelivery] AS delivery
    ON delivery.[ActivityId] = release.[ActivityId];
