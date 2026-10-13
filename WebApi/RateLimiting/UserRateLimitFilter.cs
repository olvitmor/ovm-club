using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace WebApi.RateLimiting;

public sealed class UserRateLimitFilter : IAsyncResourceFilter
{
    private readonly UserRateLimiter _limiter;
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public UserRateLimitFilter(UserRateLimiter limiter, ProblemDetailsFactory problemDetailsFactory)
    {
        _limiter = limiter;
        _problemDetailsFactory = problemDetailsFactory;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var userIdClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userIdClaim == null || !long.TryParse(userIdClaim, CultureInfo.InvariantCulture, out var userId))
        {
            await next();

            return;
        }

        var retryAfter = await _limiter.TryAcquireAsync(userId);

        if (retryAfter == null)
        {
            await next();

            return;
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.Value.TotalSeconds));
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        var problem = _problemDetailsFactory.CreateProblemDetails(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            detail: $"Too many requests, retry in {seconds} s");

        context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status429TooManyRequests };
    }
}
