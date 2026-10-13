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

namespace BusinessLogic.Enrollments.CreateEnrollment;

public sealed class CreateEnrollmentCommandHandler : ICommandHandler<CreateEnrollmentCommand, Result<CreatedEnrollment>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IdempotencyOptions _idempotencyOptions;

    public CreateEnrollmentCommandHandler(
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<IdempotencyOptions> idempotencyOptions)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _idempotencyOptions = idempotencyOptions.Value;
    }

    public async Task<Result<CreatedEnrollment>> HandleAsync(CreateEnrollmentCommand command, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.DbContext.Database.BeginTransactionAsync(ct);

        var now = _timeProvider.GetUtcNowTruncated();
        var requestHash = IdempotencyHash.Of($"{command.ClassId}:{command.Seats}");

        // решение: в key хранится SHA-256 ключа — длина ключа по контракту не ограничена, а запись btree-индекса ограничена.
        var idempotencyKey = await _unitOfWork.IdempotencyKey.TryAcquireAsync(
            new IdempotencyKey
            {
                UserId = command.UserId,
                Operation = IdempotencyOperation.CreateEnrollment,
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
                    IdempotencyOperation.CreateEnrollment,
                    IdempotencyHash.Of(command.IdempotencyKey),
                    ct)
                ?? throw new InvalidOperationException(
                    $"Idempotency key of user {command.UserId} for CreateEnrollment neither acquired nor found");

            if (stored.RequestHash != requestHash)
            {
                return ConflictReason.IdempotencyKeyReused;
            }

            return JsonSerializer.Deserialize<CreatedEnrollment>(
                    stored.ResponseBody
                        ?? throw new InvalidOperationException($"Idempotency key {stored.Id} has no response"))
                ?? throw new InvalidOperationException($"Idempotency key {stored.Id} has empty response");
        }

        var scheduledClass = await _unitOfWork.Class.LockAsync(command.ClassId, ct);

        if (scheduledClass == null)
        {
            return Result<CreatedEnrollment>.NotFound();
        }

        // решение: при нескольких причинах отказа отвечаем первой по порядку «занятие → абонемент → места»:
        // сначала то, что само не исправится, последней — вместимость, которая может освободиться.
        var classConflict = BookingPolicy.CheckClassOpen(scheduledClass.IsCancelled, scheduledClass.StartsAt, now);

        if (classConflict != null)
        {
            return classConflict.Value;
        }

        var pass = await _unitOfWork.Pass.LockByUserAsync(command.UserId, ct);

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

        var created = new CreatedEnrollment(
            enrollment.Id,
            enrollment.ClassId,
            enrollment.Seats,
            enrollment.Status,
            enrollment.CreatedAt);

        idempotencyKey.ResponseBody = JsonSerializer.Serialize(created);

        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return created;
    }
}
