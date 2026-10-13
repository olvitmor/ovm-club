using BusinessLogic.Results;

namespace BusinessLogic.Booking;

public static class BookingPolicy
{
    public static ConflictReason? CheckClassOpen(bool isCancelled, DateTime startsAt, DateTime now)
    {
        if (isCancelled)
        {
            return ConflictReason.ClassCancelled;
        }

        return now >= startsAt ? ConflictReason.ClassStarted : null;
    }

    public static ConflictReason? CheckCancellable(bool isClassCancelled, DateTime startsAt, DateTime now)
    {
        return !isClassCancelled && now >= startsAt ? ConflictReason.ClassStarted : null;
    }

    public static int RefundedVisits(bool isClassCancelled, DateTime startsAt, DateTime now, int seats, TimeSpan refundCutoff)
    {
        return isClassCancelled || startsAt - now >= refundCutoff ? seats : 0;
    }

    public static ConflictReason? CheckPassCovers(DateOnly validUntil, int remainingVisits, int visits, DateOnly today)
    {
        if (validUntil < today)
        {
            return ConflictReason.PassExpired;
        }

        return remainingVisits < visits ? ConflictReason.PassExhausted : null;
    }

    public static ConflictReason? CheckCapacity(int capacity, int takenSeats, int requestedSeats)
    {
        return (long)takenSeats + requestedSeats > capacity ? ConflictReason.CapacityExceeded : null;
    }
}
