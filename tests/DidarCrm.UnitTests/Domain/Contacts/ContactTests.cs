using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Contacts;

namespace DidarCrm.UnitTests.Domain.Contacts;

public sealed class ContactTests
{
    [Fact]
    public void AdvanceLastActivityAt_moves_the_timestamp_forward()
    {
        var initialTimestamp = new DateTimeOffset(
            2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        var laterTimestamp = initialTimestamp.AddMinutes(5);
        var contact = Contact.Create(
            ContactId.From(Guid.NewGuid()),
            TenantId.From(Guid.NewGuid()),
            initialTimestamp);

        contact.AdvanceLastActivityAt(laterTimestamp);

        Assert.Equal(laterTimestamp, contact.LastActivityAt);
    }

    [Fact]
    public void AdvanceLastActivityAt_does_not_move_the_timestamp_backward()
    {
        var currentTimestamp = new DateTimeOffset(
            2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        var contact = Contact.Create(
            ContactId.From(Guid.NewGuid()),
            TenantId.From(Guid.NewGuid()),
            currentTimestamp);

        contact.AdvanceLastActivityAt(currentTimestamp.AddMinutes(-5));

        Assert.Equal(currentTimestamp, contact.LastActivityAt);
    }
}
