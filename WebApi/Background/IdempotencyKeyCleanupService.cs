using BusinessLogic.Options;
using BusinessLogic.Time;
using DataAccessLayer.UnitOfWork;
using Microsoft.Extensions.Options;

namespace WebApi.Background;

public sealed class IdempotencyKeyCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IdempotencyOptions _options;
    private readonly ILogger<IdempotencyKeyCleanupService> _logger;

    public IdempotencyKeyCleanupService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyKeyCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var expiredBefore = _timeProvider.GetUtcNowTruncated() - _options.Retention;
        var total = 0;
        int deleted;

        do
        {
            deleted = await unitOfWork.IdempotencyKey.DeleteExpiredAsync(expiredBefore, _options.CleanupBatchSize, ct);
            total += deleted;
        }
        while (deleted == _options.CleanupBatchSize);

        return total;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.CleanupInterval, _timeProvider);

        do
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Idempotency key cleanup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
