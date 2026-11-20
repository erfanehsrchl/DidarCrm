using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Domain.Notifications;

namespace DidarCrm.Infrastructure.Persistence.Repositories;

internal sealed class ActivityNotificationRepository(CrmDbContext dbContext)
    : IActivityNotificationRepository
{
    public void Add(ActivityNotification notification)
    {
        dbContext.Notifications.Add(notification);
    }
}
