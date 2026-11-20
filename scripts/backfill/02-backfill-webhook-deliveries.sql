SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @BatchSize int = 1000;
DECLARE @ThrottleDelay varchar(12) = '00:00:00.100';
DECLARE @RowsAffected int = 1;

IF @BatchSize <= 0
    THROW 50001, 'Batch size must be greater than zero.', 1;

WHILE @RowsAffected > 0
BEGIN
    BEGIN TRANSACTION;

    INSERT INTO [dbo].[crmWebhookDelivery]
    (
        [Id],
        [ActivityId],
        [Status],
        [CreatedAt],
        [LastAttemptAt],
        [LastError],
        [AttemptCount]
    )
    SELECT TOP (@BatchSize)
        NEWID(),
        activity.[Id],
        CASE
            WHEN activity.[WebhookSent] = 1 THEN N'Succeeded'
            ELSE N'Pending'
        END,
        activity.[CreatedAt],
        NULL,
        NULL,
        0
    FROM [dbo].[crmActivity] AS activity
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [dbo].[crmWebhookDelivery] AS delivery
            WITH (UPDLOCK, HOLDLOCK)
        WHERE delivery.[ActivityId] = activity.[Id]
    )
    ORDER BY activity.[Id];

    SET @RowsAffected = @@ROWCOUNT;

    COMMIT TRANSACTION;

    IF @RowsAffected > 0 AND @ThrottleDelay > '00:00:00.000'
        WAITFOR DELAY @ThrottleDelay;
END;
