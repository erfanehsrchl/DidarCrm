namespace DidarCrm.Api.Contracts.Activities;

public sealed record CreateActivityRequest(
    Guid ContactId,
    string? Title);
