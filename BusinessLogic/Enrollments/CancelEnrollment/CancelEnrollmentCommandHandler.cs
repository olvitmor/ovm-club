using BusinessLogic.Booking;
using BusinessLogic.Options;
using BusinessLogic.Results;
using BusinessLogic.Time;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.Extensions.Options;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.CancelEnrollment;

public sealed class CancelEnrollmentCommandHandler
    : ICommandHandler<CancelEnrollmentCommand, Result<CancelledEnrollment>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly BookingOptions _options;

    public CancelEnrollmentCommandHandler(
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<BookingOptions> options)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<Result<CancelledEnrollment>> HandleAsync(CancelEnrollmentCommand command, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.DbContext.Database.BeginTransactionAsync(ct);

        var enrollment = await _unitOfWork.Enrollment.GetByIdAsync(command.EnrollmentId, ct);

        if (enrollment == null || enrollment.UserId != command.UserId)
        {
            return Result<CancelledEnrollment>.NotFound();
        }

        var scheduledClass = await _unitOfWork.Class.LockAsync(enrollment.ClassId, ct)
            ?? throw new InvalidOperationException(
                $"Class {enrollment.ClassId} of enrollment {enrollment.Id} not found");

        await _unitOfWork.DbContext.Entry(enrollment).ReloadAsync(ct);

        // решение: повторная отмена отдаёт сохранённый возврат, а не пересчитывает его — граница могла уже пройти.
        if (enrollment.Status == EnrollmentStatus.Cancelled)
        {
            return new CancelledEnrollment(
                enrollment.Id,
                enrollment.Status,
                enrollment.RefundedVisits
                    ?? throw new InvalidOperationException($"Cancelled enrollment {enrollment.Id} has no refunded visits"));
        }

        var now = _timeProvider.GetUtcNowTruncated();
        var conflict = BookingPolicy.CheckCancellable(scheduledClass.IsCancelled, scheduledClass.StartsAt, now);

        if (conflict != null)
        {
            return conflict.Value;
        }

        var refundedVisits = BookingPolicy.RefundedVisits(
            scheduledClass.IsCancelled,
            scheduledClass.StartsAt,
            now,
            enrollment.Seats,
            _options.RefundCutoff);

        if (refundedVisits > 0)
        {
            var pass = await _unitOfWork.Pass.LockByUserAsync(command.UserId, ct);
            pass.RemainingVisits += refundedVisits;
        }

        enrollment.Status = EnrollmentStatus.Cancelled;
        enrollment.CancelledAt = now;
        enrollment.RefundedVisits = refundedVisits;

        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new CancelledEnrollment(enrollment.Id, enrollment.Status, refundedVisits);
    }
}
