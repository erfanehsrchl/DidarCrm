using DidarCrm.Api.Contracts;
using DidarCrm.Api.Contracts.Activities;
using DidarCrm.Application.Activities.CreateActivity;
using Microsoft.AspNetCore.Mvc;

namespace DidarCrm.Api.Controllers;

[Route("api/v1/activities")]
public sealed class ActivitiesController(CreateActivityHandler handler)
    : BaseController
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateActivityResponse>),StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CreateActivityResponse>),StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CreateActivityResponse>),StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<CreateActivityResponse>),StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateActivityRequest request,
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateActivityCommand(
                tenantId,
                request.ContactId,
                idempotencyKey,
                request.Title ?? string.Empty),
            cancellationToken);

        return HandleResult(
            result,
            value => new CreateActivityResponse(
                value.ActivityId,
                value.CreatedAtUtc));
    }
}
