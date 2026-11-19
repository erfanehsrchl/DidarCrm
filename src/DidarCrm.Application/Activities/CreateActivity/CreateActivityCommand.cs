namespace DidarCrm.Application.Activities.CreateActivity;

public sealed record CreateActivityCommand(
    Guid TenantId,
    Guid ContactId,
    string RequestKey,
    string Title);
