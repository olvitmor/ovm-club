using BusinessLogic.Booking;
using BusinessLogic.Enrollments.CommonModels;
using BusinessLogic.Enrollments.Helpers;
using BusinessLogic.Enrollments.Services;
using BusinessLogic.Options;
using BusinessLogic.Results;
using BusinessLogic.Time;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.Extensions.Options;
using Ovm.Cqrs;

namespace BusinessLogic.Enrollments.ChangeEnrollmentSeats;

public sealed class ChangeEnrollmentSeatsCommandHandler
    : ICommandHandler<ChangeEnrollmentSeatsCommand, Result<EnrollmentCard>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly BookingOptions _options;
    private readonly EnrollmentCardCache _cardCache;

    public ChangeEnrollmentSeatsCommandHandler(
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
        var changedUserIds = new List<long>();

        if (difference > 0)
        {
            var pass = await _unitOfWork.Pass.LockByUserAsync(command.UserId, ct);

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

            enrollment.Seats = command.Seats;
            pass.RemainingVisits -= difference;

            await _unitOfWork.SaveChangesAsync(ct);

            changedUserIds.Add(command.UserId);
        }
        else if (difference < 0)
        {
            enrollment.Seats = command.Seats;

            await _unitOfWork.SaveChangesAsync(ct);

            var waitlist = new List<WaitlistEntry>();
            var freeSeats = 0;

            if (WaitlistPolicy.IsOpen(scheduledClass.IsCancelled, scheduledClass.StartsAt, now, _options.WaitlistCloseBefore))
            {
                // решение: в очередь уходят все свободные места занятия, а не только освобождённые этой операцией.
                freeSeats = scheduledClass.Capacity - await _unitOfWork.Enrollment.GetActiveSeatsAsync(scheduledClass.Id, ct);
                waitlist = await _unitOfWork.Waitlist.GetWaitingAsync(scheduledClass.Id, freeSeats, ct);
            }

            var passes = (await _unitOfWork.Pass.LockByUsersAsync(
                    waitlist.Select(x => x.UserId).Append(command.UserId).ToList(),
                    ct))
                .ToDictionary(x => x.UserId);

            // решение: возврат начисляется до перевода — если инициатор сам ждёт на этом занятии,
            // он переводится на общих правилах и может сразу потратить вернувшиеся посещения.
            passes[command.UserId].RemainingVisits -= difference;

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

            changedUserIds.AddRange(promotions.Select(x => x.Entry.UserId).Append(command.UserId));
        }

        var card = await EnrollmentCardReader.ReadAsync(_unitOfWork, command.UserId, command.EnrollmentId, ct)
            ?? throw new InvalidOperationException($"Card of enrollment {command.EnrollmentId} not found after change");

        await transaction.CommitAsync(ct);
        await _cardCache.InvalidateAsync(changedUserIds, ct);

        return card;
    }
}
