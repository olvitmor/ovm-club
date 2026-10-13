using System.Text.Json;
using BusinessLogic.Booking;
using BusinessLogic.Idempotency;
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
    private readonly IdempotencyOptions _idempotencyOptions;

    public JoinWaitlistCommandHandler(
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<BookingOptions> options,
        IOptions<IdempotencyOptions> idempotencyOptions)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
        _idempotencyOptions = idempotencyOptions.Value;
    }

    public async Task<Result<JoinedWaitlist>> HandleAsync(JoinWaitlistCommand command, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.DbContext.Database.BeginTransactionAsync(ct);

        var now = _timeProvider.GetUtcNowTruncated();
        var requestHash = IdempotencyHash.Of($"{command.ClassId}:{command.Seats}");

        // решение: в key хранится SHA-256 ключа — длина ключа по контракту не ограничена, а запись btree-индекса ограничена.
        var idempotencyKey = await _unitOfWork.IdempotencyKey.TryAcquireAsync(
            new IdempotencyKey
            {
                UserId = command.UserId,
                Operation = IdempotencyOperation.JoinWaitlist,
                Key = IdempotencyHash.Of(command.IdempotencyKey),
                RequestHash = requestHash,
                CreatedAt = now
            },
            now - _idempotencyOptions.Retention,
            ct);

        if (idempotencyKey == null)
        {
            var stored = await _unitOfWork.IdempotencyKey.GetAsync(
                    command.UserId,
                    IdempotencyOperation.JoinWaitlist,
                    IdempotencyHash.Of(command.IdempotencyKey),
                    ct)
                ?? throw new InvalidOperationException(
                    $"Idempotency key of user {command.UserId} for JoinWaitlist neither acquired nor found");

            if (stored.RequestHash != requestHash)
            {
                return ConflictReason.IdempotencyKeyReused;
            }

            return JsonSerializer.Deserialize<JoinedWaitlist>(
                    stored.ResponseBody
                        ?? throw new InvalidOperationException($"Idempotency key {stored.Id} has no response"))
                ?? throw new InvalidOperationException($"Idempotency key {stored.Id} has empty response");
        }

        var scheduledClass = await _unitOfWork.Class.LockAsync(command.ClassId, ct);

        if (scheduledClass == null)
        {
            return Result<JoinedWaitlist>.NotFound();
        }

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

        // решение: абонемент при постановке не проверяется и посещения не списываются — в контракте очереди
        // нет причин pass-*, а посещения списываются при переводе в бронь.
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

        var joined = new JoinedWaitlist(
            entry.Id,
            entry.ClassId,
            entry.Seats,
            await _unitOfWork.Waitlist.GetPositionAsync(entry, ct),
            entry.CreatedAt);

        idempotencyKey.ResponseBody = JsonSerializer.Serialize(joined);

        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return joined;
    }
}
