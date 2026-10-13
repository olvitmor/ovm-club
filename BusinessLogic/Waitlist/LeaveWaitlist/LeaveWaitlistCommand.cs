using BusinessLogic.Results;
using Ovm.Cqrs;

namespace BusinessLogic.Waitlist.LeaveWaitlist;

public sealed record LeaveWaitlistCommand(long UserId, long EntryId) : ICommand<Result<LeftWaitlist>>;
