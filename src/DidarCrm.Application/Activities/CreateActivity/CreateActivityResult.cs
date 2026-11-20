namespace DidarCrm.Application.Activities.CreateActivity;

public sealed record CreateActivityResult(
    Guid ActivityId,
    DateTimeOffset CreatedAtUtc);
