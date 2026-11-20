using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;
using DidarCrm.Domain.Notifications;
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

        var tableNames = dbContext.Model
            .GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .ToHashSet();

        Assert.Contains("InboxState", tableNames);
        Assert.Contains("OutboxMessage", tableNames);
        Assert.Contains("OutboxState", tableNames);
        Assert.Contains("crmNotification", tableNames);
    }
}
