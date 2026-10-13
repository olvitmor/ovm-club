using Microsoft.AspNetCore.Mvc;

namespace WebApi.RateLimiting;

public sealed class UserRateLimitAttribute : ServiceFilterAttribute
{
    public UserRateLimitAttribute()
        : base(typeof(UserRateLimitFilter))
    {
    }
}
