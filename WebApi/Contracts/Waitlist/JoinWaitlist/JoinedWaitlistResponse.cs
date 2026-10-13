using BusinessLogic.Waitlist.JoinWaitlist;

namespace WebApi.Contracts.Waitlist.JoinWaitlist;

public record JoinedWaitlistResponse(long Id, long ClassId, int Seats, int Position, DateTime CreatedAt)
{
    public static JoinedWaitlistResponse From(JoinedWaitlist entry)
    {
        return new JoinedWaitlistResponse(entry.Id, entry.ClassId, entry.Seats, entry.Position, entry.CreatedAt);
    }
}
