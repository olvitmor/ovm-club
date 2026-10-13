using BusinessLogic.Options;
using BusinessLogic.Schedule.GetSchedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ovm.Cqrs;
using WebApi.Contracts.Schedule.GetSchedule;
using WebApi.RateLimiting;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[UserRateLimit]
[Route("schedule")]
public class ScheduleController : BaseApiController
{
    private readonly IQueryProcessor _queries;
    private readonly ScheduleOptions _options;

    public ScheduleController(IQueryProcessor queries, IOptions<ScheduleOptions> options)
    {
        _queries = queries;
        _options = options.Value;
    }

    [HttpGet]
    [ProducesResponseType<ScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get([FromQuery] GetScheduleRequest request, CancellationToken ct)
    {
        var from = request.From!.Value;
        var to = request.To!.Value;
        var limit = request.Limit ?? _options.DefaultLimit;
        ScheduleCursor? after = null;

        if (to <= from)
        {
            ModelState.AddModelError(nameof(request.To), "to must be later than from");
        }

        if (limit < 1 || limit > _options.MaxLimit)
        {
            ModelState.AddModelError(nameof(request.Limit), $"limit must be between 1 and {_options.MaxLimit}");
        }

        if (request.ContinuationToken != null
            && !ScheduleToken.TryDecode(request.ContinuationToken, from, to, request.RoomId, out after))
        {
            ModelState.AddModelError(
                nameof(request.ContinuationToken),
                "continuationToken is invalid or was issued for other filters");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var page = await _queries.ProcessQueryAsync(
            new GetScheduleQuery(CurrentUserId, from, to, request.RoomId, limit, after),
            ct);

        return Ok(new ScheduleResponse(
            page.Items.Select(ScheduleItemResponse.From).ToList(),
            page.Next == null ? null : ScheduleToken.Encode(page.Next, from, to, request.RoomId)));
    }
}
