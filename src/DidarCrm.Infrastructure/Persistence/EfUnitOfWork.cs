using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Infrastructure.Persistence.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DidarCrm.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(CrmDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConcurrencyException(exception);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateRequestKeyViolation(exception))
        {
            throw new DuplicateRequestKeyException(exception);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateWebhookDeliveryViolation(exception))
        {
            foreach (var entry in exception.Entries)
            {
                entry.State = EntityState.Detached;
            }

            throw new DuplicateWebhookDeliveryException(exception);
        }
    }

    private static bool IsDuplicateWebhookDeliveryViolation(
        DbUpdateException exception)
    {
        if (exception.InnerException is not SqlException sqlException)
        {
            return false;
        }

        return sqlException.Errors
            .Cast<SqlError>()
            .Any(error =>
                error.Number is 2601 or 2627 &&
                error.Message.Contains(
                    WebhookDeliveryConfiguration.ActivityIdUniqueIndexName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDuplicateRequestKeyViolation(
        DbUpdateException exception)
    {
        if (exception.InnerException is not SqlException sqlException)
        {
            return false;
        }

        return sqlException.Errors
            .Cast<SqlError>()
            .Any(error =>
                error.Number is 2601 or 2627 &&
                error.Message.Contains(
                    CustomerActivityConfiguration.RequestKeyUniqueIndexName,
                    StringComparison.OrdinalIgnoreCase));
    }
}
