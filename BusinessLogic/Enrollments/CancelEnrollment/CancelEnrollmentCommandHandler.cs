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
    private readonly EnrollmentCardCache _cardCache;

    public CancelEnrollmentCommandHandler(
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<BookingOptions> options,
        EnrollmentCardCache cardCache)
    {
        _cardCache = cardCache;
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

        enrollment.Status = EnrollmentStatus.Cancelled;
        enrollment.CancelledAt = now;
        enrollment.RefundedVisits = refundedVisits;

        await _unitOfWork.SaveChangesAsync(ct);

        var waitlist = new List<WaitlistEntry>();
        var freeSeats = 0;

        if (WaitlistPolicy.IsOpen(scheduledClass.IsCancelled, scheduledClass.StartsAt, now, _options.WaitlistCloseBefore))
        {
            freeSeats = scheduledClass.Capacity - await _unitOfWork.Enrollment.GetActiveSeatsAsync(scheduledClass.Id, ct);
            waitlist = await _unitOfWork.Waitlist.GetWaitingAsync(scheduledClass.Id, freeSeats, ct);
        }

        var userIds = waitlist.Select(x => x.UserId).ToList();

        if (refundedVisits > 0)
        {
            userIds.Add(command.UserId);
        }

        var passes = (await _unitOfWork.Pass.LockByUsersAsync(userIds, ct)).ToDictionary(x => x.UserId);

        // решение: возврат начисляется до перевода — если инициатор сам ждёт на этом занятии,
        // он переводится на общих правилах и может сразу потратить вернувшиеся посещения.
        if (refundedVisits > 0)
        {
            passes[command.UserId].RemainingVisits += refundedVisits;
        }

        var promotedIds = WaitlistPolicy.SelectPromotions(
            waitlist.Select(x => new WaitlistCandidate(
                x.Id,
                x.Seats,
                passes[x.UserId].ValidUntil,
                passes[x.UserId].RemainingVisits)),
            freeSeats,
            _timeProvider.GetUtcToday());

        var promotions = new List<(WaitlistEntry Entry, Enrollment Enrollment)>();

        foreach (var entry in waitlist.Where(x => promotedIds.Contains(x.Id)))
        {
            var promoted = await _unitOfWork.Enrollment.CreateAsync(
                new Enrollment
                {
                    UserId = entry.UserId,
                    ClassId = scheduledClass.Id,
                    Seats = entry.Seats,
                    Status = EnrollmentStatus.Active,
                    CreatedAt = now
                },
                ct);

            entry.Status = WaitlistStatus.Promoted;
            passes[entry.UserId].RemainingVisits -= entry.Seats;
            promotions.Add((entry, promoted));
        }

        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var (entry, promoted) in promotions)
        {
            entry.EnrollmentId = promoted.Id;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await _cardCache.InvalidateAsync(promotions.Select(x => x.Entry.UserId).Append(command.UserId), ct);

        return new CancelledEnrollment(enrollment.Id, enrollment.Status, refundedVisits);
    }
}
