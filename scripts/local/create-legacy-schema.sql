IF DB_ID(N'DidarCrm') IS NULL
BEGIN
    CREATE DATABASE [DidarCrm];
END;
GO

USE [DidarCrm];
GO

IF OBJECT_ID(N'[dbo].[cntContact]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[cntContact]
    (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [LastActivityAt] datetimeoffset NULL,
        CONSTRAINT [PK_cntContact] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[crmActivity]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[crmActivity]
    (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ContactId] uniqueidentifier NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [WebhookSent] bit NOT NULL
            CONSTRAINT [DF_crmActivity_WebhookSent] DEFAULT 0,
        [NotificationCreated] bit NOT NULL
            CONSTRAINT [DF_crmActivity_NotificationCreated] DEFAULT 0,
        CONSTRAINT [PK_crmActivity] PRIMARY KEY ([Id])
    );
END;
GO
