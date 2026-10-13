using BusinessLogic.Enrollments.Services;
using Microsoft.Extensions.DependencyInjection;
using Ovm.Cqrs;

namespace BusinessLogic;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddSingleton<EnrollmentCardCache>();
        services.AddCqrs(cqrs => cqrs.AddHandlersFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

        return services;
    }
}
