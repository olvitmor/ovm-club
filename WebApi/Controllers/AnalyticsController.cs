using BusinessLogic.Analytics.GetRoomLoad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ovm.Cqrs;
using WebApi.Contracts.Analytics.GetRoomLoad;

namespace WebApi.Controllers;

[Authorize(Roles = AdminRole)]
[ApiController]
[Route("analytics")]
public class AnalyticsController : BaseApiController
{
    private readonly IQueryProcessor _queries;

    public AnalyticsController(IQueryProcessor queries)
    {
        _queries = queries;
    }

    [HttpGet("load")]
    [ProducesResponseType<RoomLoadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLoad([FromQuery] GetRoomLoadRequest request, CancellationToken ct)
    {
        var from = request.From!.Value;
        var to = request.To!.Value;

        if (to < from)
        {
            ModelState.AddModelError(nameof(request.To), "to must not be earlier than from");

            return ValidationProblem(ModelState);
        }

        var load = await _queries.ProcessQueryAsync(new GetRoomLoadQuery(from, to, request.RoomId), ct);

        return Ok(new RoomLoadResponse(load.Select(RoomLoadItemResponse.From).ToList()));
    }
}
