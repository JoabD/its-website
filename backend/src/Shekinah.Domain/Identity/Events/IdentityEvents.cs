using Shekinah.Domain.Common;

namespace Shekinah.Domain.Identity.Events;

public sealed record UserCreated(Guid EventId, DateTime OccurredOnUtc, string UserId, int EnrollmentNumber, string Role) : IDomainEvent;

public sealed record UserBlockedForDelinquency(Guid EventId, DateTime OccurredOnUtc, string UserId, int NoticeCount) : IDomainEvent;

public sealed record UserPasswordReset(Guid EventId, DateTime OccurredOnUtc, string UserId, string ResetByUserId) : IDomainEvent;
