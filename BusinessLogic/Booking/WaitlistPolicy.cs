namespace BusinessLogic.Booking;

public static class WaitlistPolicy
{
    public static bool IsOpen(bool isClassCancelled, DateTime startsAt, DateTime now, TimeSpan closeBefore)
    {
        return !isClassCancelled && now < startsAt - closeBefore;
    }

    public static IReadOnlyList<long> SelectPromotions(
        IEnumerable<WaitlistCandidate> queue,
        int freeSeats,
        DateOnly today)
    {
        var promoted = new List<long>();

        foreach (var candidate in queue)
        {
            if (candidate.Seats > freeSeats
                || BookingPolicy.CheckPassCovers(
                    candidate.PassValidUntil,
                    candidate.PassRemainingVisits,
                    candidate.Seats,
                    today) != null)
            {
                continue;
            }

            promoted.Add(candidate.EntryId);
            freeSeats -= candidate.Seats;
        }

        return promoted;
    }
}
