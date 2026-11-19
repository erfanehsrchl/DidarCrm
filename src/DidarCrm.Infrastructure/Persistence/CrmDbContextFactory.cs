using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DidarCrm.Infrastructure.Persistence;

internal sealed class CrmDbContextFactory
    : IDesignTimeDbContextFactory<CrmDbContext>
{
    private const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=DidarCrm;Trusted_Connection=True;TrustServerCertificate=True";

    public CrmDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__Database") ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new CrmDbContext(options);
    }
}
