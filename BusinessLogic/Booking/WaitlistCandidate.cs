namespace BusinessLogic.Booking;

public record WaitlistCandidate(long EntryId, int Seats, DateOnly PassValidUntil, int PassRemainingVisits);
