using BusinessLogic.Waitlist.LeaveWaitlist;

namespace WebApi.Contracts.Waitlist.LeaveWaitlist;

public record LeftWaitlistResponse(long Id, string Status)
{
    public static LeftWaitlistResponse From(LeftWaitlist entry)
    {
        return new LeftWaitlistResponse(entry.Id, entry.Status);
    }
}
