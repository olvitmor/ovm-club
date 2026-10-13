using BusinessLogic.Booking;
using BusinessLogic.Results;
using BusinessLogic.Time;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.CreateEnrollment;

public sealed class CreateEnrollmentCommandHandler : ICommandHandler<CreateEnrollmentCommand, Result<CreatedEnrollment>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateEnrollmentCommandHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CreatedEnrollment>> HandleAsync(CreateEnrollmentCommand command, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.DbContext.Database.BeginTransactionAsync(ct);

        var scheduledClass = await _unitOfWork.Class.LockAsync(command.ClassId, ct);

        if (scheduledClass == null)
        {
            return Result<CreatedEnrollment>.NotFound();
        }

        var now = _timeProvider.GetUtcNowTruncated();

        // решение: при нескольких причинах отказа отвечаем первой по порядку «занятие → абонемент → места»:
        // сначала то, что само не исправится, последней — вместимость, которая может освободиться.
        var classConflict = BookingPolicy.CheckClassOpen(scheduledClass.IsCancelled, scheduledClass.StartsAt, now);

        if (classConflict != null)
        {
            return classConflict.Value;
        }

        var pass = (await _unitOfWork.Pass.LockByUsersAsync([command.UserId], ct)).Single();

        var passConflict = BookingPolicy.CheckPassCovers(
            pass.ValidUntil,
            pass.RemainingVisits,
            command.Seats,
            _timeProvider.GetUtcToday());

        if (passConflict != null)
        {
            return passConflict.Value;
        }

        var takenSeats = await _unitOfWork.Enrollment.GetActiveSeatsAsync(scheduledClass.Id, ct);
        var capacityConflict = BookingPolicy.CheckCapacity(scheduledClass.Capacity, takenSeats, command.Seats);

        if (capacityConflict != null)
        {
            return capacityConflict.Value;
        }

        var enrollment = await _unitOfWork.Enrollment.CreateAsync(
            new Enrollment
            {
                UserId = command.UserId,
                ClassId = scheduledClass.Id,
                Seats = command.Seats,
                Status = EnrollmentStatus.Active,
                CreatedAt = now
            },
            ct);

        pass.RemainingVisits -= command.Seats;

        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new CreatedEnrollment(
            enrollment.Id,
            enrollment.ClassId,
            enrollment.Seats,
            enrollment.Status,
            enrollment.CreatedAt);
    }
}
