using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Tests.Infrastructure;

namespace WebApi.Tests.Repositories;

public abstract class RepositoryTestBase
{
    private static readonly TimeSpan BlockingProbe = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromSeconds(10);

    private static readonly Lazy<ApiApplicationFactory> SharedFactory = new(() =>
    {
        var factory = new ApiApplicationFactory();
        factory.CreateClient().Dispose();

        return factory;
    });

    protected static ApiApplicationFactory Factory => SharedFactory.Value;

    protected static Given Given => Factory.Given;

    protected static async Task<T> InUnitOfWork<T>(Func<IUnitOfWork, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();

        return await action(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
    }

    protected static async Task<bool> SecondTransactionWaits(
        Func<IUnitOfWork, Task> first,
        Func<IUnitOfWork, Task> second)
    {
        await using var firstScope = Factory.Services.CreateAsyncScope();
        await using var secondScope = Factory.Services.CreateAsyncScope();
        var firstUnitOfWork = firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var secondUnitOfWork = secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await using var firstTransaction = await firstUnitOfWork.DbContext.Database.BeginTransactionAsync();
        await first(firstUnitOfWork);

        await using var secondTransaction = await secondUnitOfWork.DbContext.Database.BeginTransactionAsync();
        var secondTask = second(secondUnitOfWork);

        var waited = await Task.WhenAny(secondTask, Task.Delay(BlockingProbe)) != secondTask;

        await firstTransaction.CommitAsync();
        await secondTask.WaitAsync(ReleaseTimeout);
        await secondTransaction.RollbackAsync();

        return waited;
    }
}
