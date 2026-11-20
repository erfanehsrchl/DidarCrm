using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using DidarCrm.Domain.Notifications;
using DidarCrm.Domain.Webhooks;
using DidarCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.IntegrationTests.Persistence;

public sealed class CrmDbContextModelTests
{
    [Fact]
    public void Model_maps_the_legacy_activity_and_contact_tables()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new CrmDbContext(options);

        var activity = dbContext.Model.FindEntityType(typeof(CustomerActivity));

        Assert.NotNull(activity);
        Assert.Equal("crmActivity", activity.GetTableName());
        Assert.Equal(
            RequestKey.MaxLength,
            activity.FindProperty(nameof(CustomerActivity.RequestKey))?.GetMaxLength());
        Assert.True(activity.FindProperty(nameof(CustomerActivity.RequestKey))?.IsNullable);
        Assert.Equal(
            ActivityTitle.MaxLength,
            activity.FindProperty(nameof(CustomerActivity.Title))?.GetMaxLength());
        Assert.Equal(
            "CreatedAt",
            activity.FindProperty(nameof(CustomerActivity.CreatedAtUtc))?.GetColumnName());

        var requestKeyIndex = Assert.Single(
            activity.GetIndexes(),
            index => index.GetDatabaseName() ==
                     "UX_crmActivity_TenantId_RequestKey");

        Assert.True(requestKeyIndex.IsUnique);
        Assert.Equal("[RequestKey] IS NOT NULL", requestKeyIndex.GetFilter());
        Assert.Collection(
            requestKeyIndex.Properties,
            property => Assert.Equal(nameof(CustomerActivity.TenantId), property.Name),
            property => Assert.Equal(nameof(CustomerActivity.RequestKey), property.Name));

        var contact = dbContext.Model.FindEntityType(typeof(Contact));

        Assert.NotNull(contact);
        Assert.Equal("cntContact", contact.GetTableName());
        Assert.NotNull(contact.FindProperty("Id"));
        Assert.NotNull(contact.FindProperty("TenantId"));
        Assert.True(contact.FindProperty("LastActivityAt")?.IsConcurrencyToken);

        var notification = dbContext.Model.FindEntityType(
            typeof(ActivityNotification));

        Assert.NotNull(notification);
        Assert.Equal("crmNotification", notification.GetTableName());
        Assert.Equal(3, notification.GetProperties().Count());

        var activityIdIndex = Assert.Single(
            notification.GetIndexes(),
            index => index.GetDatabaseName() ==
                "UX_crmNotification_ActivityId");

        Assert.True(activityIdIndex.IsUnique);
        Assert.Equal(
            nameof(ActivityNotification.ActivityId),
            Assert.Single(activityIdIndex.Properties).Name);

        var webhookDelivery = dbContext.Model.FindEntityType(
            typeof(WebhookDelivery));

        Assert.NotNull(webhookDelivery);
        Assert.Equal("crmWebhookDelivery", webhookDelivery.GetTableName());
        Assert.Equal(
            nameof(WebhookDelivery.Id),
            Assert.Single(webhookDelivery.FindPrimaryKey()!.Properties).Name);
        var webhookActivityIndex = Assert.Single(
            webhookDelivery.GetIndexes(),
            index => index.GetDatabaseName() ==
                "UX_crmWebhookDelivery_ActivityId");
        Assert.True(webhookActivityIndex.IsUnique);
        Assert.Equal(
            nameof(WebhookDelivery.ActivityId),
            Assert.Single(webhookActivityIndex.Properties).Name);
        Assert.Equal(
            32,
            webhookDelivery.FindProperty(nameof(WebhookDelivery.Status))
                ?.GetMaxLength());
        Assert.Equal(
            WebhookDelivery.MaxErrorLength,
            webhookDelivery.FindProperty(nameof(WebhookDelivery.LastError))
                ?.GetMaxLength());
        Assert.True(
            webhookDelivery.FindProperty(
                nameof(WebhookDelivery.LastAttemptAtUtc))?.IsNullable);
        Assert.False(
            webhookDelivery.FindProperty(
                nameof(WebhookDelivery.AttemptCount))?.IsNullable);

        var tableNames = dbContext.Model
            .GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .ToHashSet();

        Assert.Contains("InboxState", tableNames);
        Assert.Contains("OutboxMessage", tableNames);
        Assert.Contains("OutboxState", tableNames);
        Assert.Contains("crmNotification", tableNames);
        Assert.Contains("crmWebhookDelivery", tableNames);
    }
}
