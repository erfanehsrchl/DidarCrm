namespace DidarCrm.Api.Contracts.Activities;

public sealed record CreateActivityResponse(
    Guid ActivityId,
    DateTimeOffset CreatedAtUtc);
