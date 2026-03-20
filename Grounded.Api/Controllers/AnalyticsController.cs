using Grounded.Api.Models;
using Grounded.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Grounded.Api.Controllers;

[ApiController]
[Route("analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly AnalyticsQueryPlanService _service;
    private readonly EvalRunner _evalRunner;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(AnalyticsQueryPlanService service, EvalRunner evalRunner, ILogger<AnalyticsController> logger)
    {
        _service = service;
        _evalRunner = evalRunner;
        _logger = logger;
    }

    [HttpPost("query")]
    [ProducesResponseType(typeof(ExecuteQueryPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExecuteQueryPlanResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ExecuteQueryPlanResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ExecuteQueryPlanResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExecuteQueryPlanResponse>> ExecuteQuestion(
        [FromBody] ExecuteAnalyticsQuestionRequest? request,
        CancellationToken cancellationToken)
    {
        var question = request?.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question) || question.Length > 500)
        {
            return BadRequest(new ExecuteQueryPlanResponse(
                "error",
                Rows: null,
                Metadata: null,
                Errors: new[] { new ValidationErrorDto("invalid_request", "request body must contain question with length 1..500 characters") }));
        }

        var conversationId = request?.ConversationId?.Trim();
        if (conversationId is not null && conversationId.Length > 128)
        {
            return BadRequest(new ExecuteQueryPlanResponse(
                "error",
                Rows: null,
                Metadata: null,
                Errors: new[] { new ValidationErrorDto("invalid_request", "conversationId must not exceed 128 characters") }));
        }

        try
        {
            var result = await _service.ExecuteFromQuestionAsync(question, HttpContext.TraceIdentifier, conversationId, cancellationToken);
            return result.IsSuccess
                ? Ok(result.Response)
                : UnprocessableEntity(result.Response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ExecuteQuestion for trace {TraceId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new ExecuteQueryPlanResponse(
                "error",
                Rows: null,
                Metadata: null,
                Errors: new[] { new ValidationErrorDto("internal_error", "unexpected application failure") }));
        }
    }

    [HttpPost("eval")]
    [ProducesResponseType(typeof(EvalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvalResponse>> RunEval(CancellationToken cancellationToken)
    {
        try
        {
            var (run, comparison) = await _evalRunner.RunAsync(cancellationToken);
            return Ok(new EvalResponse(run, comparison));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in RunEval for trace {TraceId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
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
        if (request?.QueryPlan is null || string.IsNullOrWhiteSpace(request.UserQuestion))
        {
            return BadRequest(new ExecuteQueryPlanResponse(
                "error",
                Rows: null,
                Metadata: null,
                Errors: new[] { new ValidationErrorDto("invalid_request", "request body must contain queryPlan and userQuestion") }));
        }

        try
        {
            var result = await _service.ExecuteAsync(request.QueryPlan, request.UserQuestion ?? string.Empty, HttpContext.TraceIdentifier, cancellationToken);
            return result.IsSuccess
                ? Ok(result.Response)
                : UnprocessableEntity(result.Response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ExecuteQueryPlan for trace {TraceId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new ExecuteQueryPlanResponse(
                "error",
                Rows: null,
                Metadata: null,
                Errors: new[] { new ValidationErrorDto("internal_error", "unexpected application failure") }));
        }
    }
}
