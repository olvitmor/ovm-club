using System.Security.Claims;
using BusinessLogic.Results;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

public abstract class BaseApiController : ControllerBase
{
    protected const string AdminRole = "admin";

    public const string ReasonExtension = "reason";

    protected long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected bool IsAdmin => User.IsInRole(AdminRole);

    protected IActionResult ToActionResult<T>(Result<T> result, Func<T, IActionResult> onSuccess)
    {
        return result.Status switch
        {
            ResultStatus.Success => onSuccess(result.Value),
            ResultStatus.NotFound => Problem(statusCode: StatusCodes.Status404NotFound),
            ResultStatus.Conflict => Conflict(result.Reason!.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, null)
        };
    }

    protected IActionResult ToOkResult<T>(Result<T> result)
    {
        return ToActionResult(result, value => Ok(value));
    }

    private ObjectResult Conflict(ConflictReason reason)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status409Conflict);

        problem.Extensions[ReasonExtension] = reason.ToCode();

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentTypes = { "application/problem+json" }
        };
    }
}
