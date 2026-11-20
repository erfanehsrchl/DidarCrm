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

    INSERT INTO [dbo].[crmNotification]
    (
        [Id],
        [ActivityId],
        [CreatedAt]
    )
    SELECT TOP (@BatchSize)
        NEWID(),
        activity.[Id],
        activity.[CreatedAt]
    FROM [dbo].[crmActivity] AS activity
    WHERE activity.[NotificationCreated] = 1
      AND NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[crmNotification] AS notification
              WITH (UPDLOCK, HOLDLOCK)
          WHERE notification.[ActivityId] = activity.[Id]
      )
    ORDER BY activity.[Id];

    SET @RowsAffected = @@ROWCOUNT;

    COMMIT TRANSACTION;

    IF @RowsAffected > 0 AND @ThrottleDelay > '00:00:00.000'
        WAITFOR DELAY @ThrottleDelay;
END;
