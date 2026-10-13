using BusinessLogic.Booking;
using BusinessLogic.Results;
using BusinessLogic.Time;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.ChangeEnrollmentSeats;

public sealed class ChangeEnrollmentSeatsCommandHandler
    : ICommandHandler<ChangeEnrollmentSeatsCommand, Result<EnrollmentCard>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ChangeEnrollmentSeatsCommandHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<EnrollmentCard>> HandleAsync(ChangeEnrollmentSeatsCommand command, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.DbContext.Database.BeginTransactionAsync(ct);

        var enrollment = await _unitOfWork.Enrollment.GetByIdAsync(command.EnrollmentId, ct);

        if (enrollment == null || enrollment.UserId != command.UserId)
        {
            return Result<EnrollmentCard>.NotFound();
        }

        var scheduledClass = await _unitOfWork.Class.LockAsync(enrollment.ClassId, ct)
            ?? throw new InvalidOperationException(
                $"Class {enrollment.ClassId} of enrollment {enrollment.Id} not found");

        // решение: бронь перечитывается после блокировки занятия — все изменения брони идут под этой блокировкой,
        // поэтому после неё статус и число мест актуальны.
        await _unitOfWork.DbContext.Entry(enrollment).ReloadAsync(ct);

        if (enrollment.Status == EnrollmentStatus.Cancelled)
        {
            return ConflictReason.EnrollmentCancelled;
        }

        var now = _timeProvider.GetUtcNowTruncated();
        var classConflict = BookingPolicy.CheckClassOpen(scheduledClass.IsCancelled, scheduledClass.StartsAt, now);

        if (classConflict != null)
        {
            return classConflict.Value;
        }

        var difference = command.Seats - enrollment.Seats;

        if (difference != 0)
        {
            var pass = await _unitOfWork.Pass.LockByUserAsync(command.UserId, ct);

            if (difference > 0)
            {
                var passConflict = BookingPolicy.CheckPassCovers(
                    pass.ValidUntil,
                    pass.RemainingVisits,
                    difference,
                    _timeProvider.GetUtcToday());

                if (passConflict != null)
                {
                    return passConflict.Value;
                }

                var takenSeats = await _unitOfWork.Enrollment.GetActiveSeatsAsync(scheduledClass.Id, ct);
                var capacityConflict = BookingPolicy.CheckCapacity(scheduledClass.Capacity, takenSeats, difference);

                if (capacityConflict != null)
                {
                    return capacityConflict.Value;
                }
            }

            enrollment.Seats = command.Seats;
            pass.RemainingVisits -= difference;

            await _unitOfWork.SaveChangesAsync(ct);
        }

        var card = await EnrollmentCardReader.ReadAsync(_unitOfWork, command.UserId, command.EnrollmentId, ct)
            ?? throw new InvalidOperationException($"Card of enrollment {command.EnrollmentId} not found after change");

        await transaction.CommitAsync(ct);

        return card;
    }
}
