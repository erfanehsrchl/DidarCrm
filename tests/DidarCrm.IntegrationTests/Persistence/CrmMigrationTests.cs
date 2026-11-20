using DidarCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DidarCrm.IntegrationTests.Persistence;

public sealed class CrmMigrationTests
{
    [Fact]
    public void ExpandMigration_AltersLegacySchemaWithoutRecreatingLegacyTables()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(
                "Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var dbContext = new CrmDbContext(options);

        var script = dbContext.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains(
            "ALTER TABLE [crmActivity] ADD [RequestKey] nvarchar(100) NULL",
            script);
        Assert.Contains("CREATE TABLE [InboxState]", script);
        Assert.Contains("CREATE TABLE [OutboxState]", script);
        Assert.Contains("CREATE TABLE [OutboxMessage]", script);
        Assert.Contains("CREATE TABLE [crmWebhookDelivery]", script);
        Assert.Contains("[UX_crmActivity_TenantId_RequestKey]", script);
        Assert.DoesNotContain("CREATE TABLE [crmActivity]", script);
        Assert.DoesNotContain("CREATE TABLE [cntContact]", script);
    }
}
