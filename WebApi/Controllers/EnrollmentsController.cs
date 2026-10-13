using BusinessLogic.Enrollments.GetEnrollmentCard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ovm.Cqrs;
using WebApi.Contracts.Enrollments;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("enrollments")]
public class EnrollmentsController : BaseApiController
{
    private readonly IQueryProcessor _queries;

    public EnrollmentsController(IQueryProcessor queries)
    {
        _queries = queries;
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
