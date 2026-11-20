using DidarCrm.Domain.Common;

namespace DidarCrm.Domain.Notifications;

public sealed record NotificationId
{
    private NotificationId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static NotificationId New() => new(Guid.NewGuid());

    public static NotificationId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException(
                "Notification id cannot be empty.",
                nameof(value));
        }

        return new NotificationId(value);
    }
}
