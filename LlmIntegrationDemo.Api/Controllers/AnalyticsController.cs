using LlmIntegrationDemo.Api.Models;
using LlmIntegrationDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LlmIntegrationDemo.Api.Controllers;

[ApiController]
[Route("analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly AnalyticsQueryPlanService _service;

    public AnalyticsController(AnalyticsQueryPlanService service)
    {
        _service = service;
    }

    [HttpPost("query-plan")]
    [ProducesResponseType(typeof(ExecuteQueryPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExecuteQueryPlanResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ExecuteQueryPlanResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ExecuteQueryPlanResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExecuteQueryPlanResponse>> ExecuteQueryPlan(
        [FromBody] ExecuteQueryPlanRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.QueryPlan is null)
        {
            return BadRequest(new ExecuteQueryPlanResponse(
                "error",
                Rows: null,
                Metadata: null,
                Errors: new[] { new ValidationErrorDto("invalid_request", "request body must contain queryPlan") }));
        }

        try
        {
            var result = await _service.ExecuteAsync(request.QueryPlan, cancellationToken);
            return result.IsSuccess
                ? Ok(result.Response)
                : UnprocessableEntity(result.Response);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ExecuteQueryPlanResponse(
                "error",
                Rows: null,
                Metadata: null,
                Errors: new[] { new ValidationErrorDto("internal_error", "unexpected application failure") }));
        }
    }
}
