using BusinessLogic.Results;
using BusinessLogic.Time;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Ovm.Cqrs;

namespace BusinessLogic.Waitlist.LeaveWaitlist;

public sealed class LeaveWaitlistCommandHandler : ICommandHandler<LeaveWaitlistCommand, Result<LeftWaitlist>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public LeaveWaitlistCommandHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LeftWaitlist>> HandleAsync(LeaveWaitlistCommand command, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.DbContext.Database.BeginTransactionAsync(ct);

        var entry = await _unitOfWork.Waitlist.GetByIdAsync(command.EntryId, ct);

        if (entry == null || entry.UserId != command.UserId)
        {
            return Result<LeftWaitlist>.NotFound();
        }

        _ = await _unitOfWork.Class.LockAsync(entry.ClassId, ct)
            ?? throw new InvalidOperationException($"Class {entry.ClassId} of waitlist entry {entry.Id} not found");

        // решение: запись перечитывается под блокировкой занятия — перевод из очереди идёт под той же блокировкой,
        // поэтому уйти из очереди и одновременно получить бронь нельзя.
        await _unitOfWork.DbContext.Entry(entry).ReloadAsync(ct);

        // решение: переведённую запись не отменяем — ответ cancelled был бы неправдой, у клиента уже бронь,
        // и отменять её нужно через отмену брони по её правилам.
        if (entry.Status == WaitlistStatus.Promoted)
        {
            return ConflictReason.AlreadyPromoted;
        }

        if (entry.Status == WaitlistStatus.Waiting)
        {
            entry.Status = WaitlistStatus.Cancelled;
            entry.CancelledAt = _timeProvider.GetUtcNowTruncated();

            await _unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        return new LeftWaitlist(entry.Id, entry.Status);
    }
}
