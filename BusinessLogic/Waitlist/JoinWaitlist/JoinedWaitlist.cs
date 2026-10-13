namespace BusinessLogic.Waitlist.JoinWaitlist;

public record JoinedWaitlist(long Id, long ClassId, int Seats, int Position, DateTime CreatedAt);
