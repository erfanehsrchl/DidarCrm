using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Application.Abstractions.Time;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Notifications;

namespace DidarCrm.Application.Notifications.CreateActivityNotification;

public sealed class CreateActivityNotificationHandler(
    IActivityNotificationRepository notificationRepository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task HandleAsync(
        CreateActivityNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        var notification = ActivityNotification.Create(
            ActivityId.From(command.ActivityId),
            clock.UtcNow);

        notificationRepository.Add(notification);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
