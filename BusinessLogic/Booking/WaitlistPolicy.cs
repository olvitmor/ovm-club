namespace BusinessLogic.Booking;

public static class WaitlistPolicy
{
    // решение: очередь закрывается за closeBefore до начала и на отменённом клубом занятии —
    // никто не получает неожиданную бронь перед самым занятием.
    public static bool IsOpen(bool isClassCancelled, DateTime startsAt, DateTime now, TimeSpan closeBefore)
    {
        return !isClassCancelled && now < startsAt - closeBefore;
    }

    // решение: места получает первый подходящий; кому не хватает мест или чей абонемент просрочен или пуст,
    // пропускается и остаётся ждать — абонемент могут пополнить вне API.
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
