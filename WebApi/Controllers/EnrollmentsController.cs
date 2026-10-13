using BusinessLogic.Enrollments.CancelEnrollment;
using BusinessLogic.Enrollments.ChangeEnrollmentSeats;
using BusinessLogic.Enrollments.CreateEnrollment;
using BusinessLogic.Enrollments.GetEnrollmentCard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ovm.Cqrs;
using WebApi.Contracts.Enrollments.CancelEnrollment;
using WebApi.Contracts.Enrollments.ChangeEnrollmentSeats;
using WebApi.Contracts.Enrollments.CreateEnrollment;
using WebApi.Contracts.Enrollments.GetEnrollmentCard;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("enrollments")]
public class EnrollmentsController : BaseApiController
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly ICommandProcessor _commands;
    private readonly IQueryProcessor _queries;

    public EnrollmentsController(ICommandProcessor commands, IQueryProcessor queries)
    {
        _commands = commands;
        _queries = queries;
    }

    [HttpPost]
    [ProducesResponseType<CreatedEnrollmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEnrollmentRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            ModelState.AddModelError(IdempotencyKeyHeader, $"{IdempotencyKeyHeader} header is required");

            return ValidationProblem(ModelState);
        }

        var command = new CreateEnrollmentCommand(CurrentUserId, request.ClassId!.Value, request.Seats!.Value);
        var result = await _commands.ProcessCommandAsync(command, ct);

        return ToActionResult(
            result,
            created => Created($"/enrollments/{created.Id}", CreatedEnrollmentResponse.From(created)));
    }

    [HttpPost("{id}/seats")]
    [ProducesResponseType<EnrollmentCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeSeats(
        long id,
        [FromBody] ChangeEnrollmentSeatsRequest request,
        CancellationToken ct)
    {
        var command = new ChangeEnrollmentSeatsCommand(CurrentUserId, id, request.Seats!.Value);
        var result = await _commands.ProcessCommandAsync(command, ct);

        return ToActionResult(result, card => Ok(EnrollmentCardResponse.From(card)));
    }

    [HttpPost("{id}/cancel")]
    [ProducesResponseType<CancelledEnrollmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        var result = await _commands.ProcessCommandAsync(new CancelEnrollmentCommand(CurrentUserId, id), ct);

        return ToActionResult(result, cancelled => Ok(CancelledEnrollmentResponse.From(cancelled)));
    }

    [HttpGet("{id}")]
    [ProducesResponseType<EnrollmentCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCard(long id, CancellationToken ct)
    {
        var result = await _queries.ProcessQueryAsync(new GetEnrollmentCardQuery(CurrentUserId, id), ct);

        return ToActionResult(result, card => Ok(EnrollmentCardResponse.From(card)));
    }
}
