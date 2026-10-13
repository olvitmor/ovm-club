using BusinessLogic.Booking;
using BusinessLogic.Options;
using BusinessLogic.Results;
using BusinessLogic.Time;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.Extensions.Options;
using Ovm.Cqrs;

namespace BusinessLogic.Waitlist.JoinWaitlist;

public sealed class JoinWaitlistCommandHandler : ICommandHandler<JoinWaitlistCommand, Result<JoinedWaitlist>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly BookingOptions _options;

    public JoinWaitlistCommandHandler(
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<BookingOptions> options)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<Result<JoinedWaitlist>> HandleAsync(JoinWaitlistCommand command, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.DbContext.Database.BeginTransactionAsync(ct);

        var scheduledClass = await _unitOfWork.Class.LockAsync(command.ClassId, ct);

        if (scheduledClass == null)
        {
            return Result<JoinedWaitlist>.NotFound();
        }

        var now = _timeProvider.GetUtcNowTruncated();
        var classConflict = BookingPolicy.CheckClassOpen(scheduledClass.IsCancelled, scheduledClass.StartsAt, now);

        if (classConflict != null)
        {
            return classConflict.Value;
        }

        if (!WaitlistPolicy.IsOpen(scheduledClass.IsCancelled, scheduledClass.StartsAt, now, _options.WaitlistCloseBefore))
        {
            return ConflictReason.WaitlistClosed;
        }

        if (await _unitOfWork.Waitlist.IsWaitingAsync(command.UserId, scheduledClass.Id, ct))
        {
            return ConflictReason.AlreadyWaiting;
        }

        var takenSeats = await _unitOfWork.Enrollment.GetActiveSeatsAsync(scheduledClass.Id, ct);

        if (BookingPolicy.CheckCapacity(scheduledClass.Capacity, takenSeats, command.Seats) == null)
        {
            return ConflictReason.SeatsAvailable;
        }

        var entry = await _unitOfWork.Waitlist.CreateAsync(
            new WaitlistEntry
            {
                UserId = command.UserId,
                ClassId = scheduledClass.Id,
                Seats = command.Seats,
                Status = WaitlistStatus.Waiting,
                CreatedAt = now
            },
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

        var position = await _unitOfWork.Waitlist.GetPositionAsync(entry, ct);

        await transaction.CommitAsync(ct);

        return new JoinedWaitlist(entry.Id, entry.ClassId, entry.Seats, position, entry.CreatedAt);
    }
}
