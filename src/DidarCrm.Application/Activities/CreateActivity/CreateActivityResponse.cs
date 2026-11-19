namespace DidarCrm.Application.Activities.CreateActivity;

public sealed record CreateActivityResponse(
    Guid ActivityId,
    DateTimeOffset CreatedAtUtc);
