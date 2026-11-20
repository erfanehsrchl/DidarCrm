using DidarCrm.Domain.Notifications;

namespace DidarCrm.Application.Abstractions.Persistence;

public interface IActivityNotificationRepository
{
    void Add(ActivityNotification notification);
}
