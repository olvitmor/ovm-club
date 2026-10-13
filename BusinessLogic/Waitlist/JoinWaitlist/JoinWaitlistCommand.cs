using BusinessLogic.Results;
using Ovm.Cqrs;

namespace BusinessLogic.Waitlist.JoinWaitlist;

public sealed record JoinWaitlistCommand(long UserId, long ClassId, int Seats, string IdempotencyKey) : ICommand<Result<JoinedWaitlist>>;
