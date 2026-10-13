using BusinessLogic.Waitlist.JoinWaitlist;
using BusinessLogic.Waitlist.LeaveWaitlist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ovm.Cqrs;
using WebApi.Contracts.Waitlist.JoinWaitlist;
using WebApi.Contracts.Waitlist.LeaveWaitlist;
using WebApi.RateLimiting;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[UserRateLimit]
[Route("enrollments/waitlist")]
public class WaitlistController : BaseApiController
{
    private readonly ICommandProcessor _commands;

    public WaitlistController(ICommandProcessor commands)
    {
        _commands = commands;
    }

    [HttpPost]
    [ProducesResponseType<JoinedWaitlistResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Join(
        [FromBody] JoinWaitlistRequest request,
        [FromHeader(Name = EnrollmentsController.IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            ModelState.AddModelError(
                EnrollmentsController.IdempotencyKeyHeader,
                $"{EnrollmentsController.IdempotencyKeyHeader} header is required");

            return ValidationProblem(ModelState);
        }

        var command = new JoinWaitlistCommand(CurrentUserId, request.ClassId!.Value, request.Seats!.Value, idempotencyKey);
        var result = await _commands.ProcessCommandAsync(command, ct);

        return ToActionResult(
            result,
            joined => StatusCode(StatusCodes.Status201Created, JoinedWaitlistResponse.From(joined)));
    }

    [HttpPost("{id}/cancel")]
    [ProducesResponseType<LeftWaitlistResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(long id, CancellationToken ct)
    {
        var result = await _commands.ProcessCommandAsync(new LeaveWaitlistCommand(CurrentUserId, id), ct);

        return ToActionResult(result, left => Ok(LeftWaitlistResponse.From(left)));
    }
}
