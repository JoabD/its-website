using Shekinah.Domain.Common;

namespace Shekinah.Domain.Academics.Events;

public sealed record AcademicPeriodOpened(Guid EventId, DateTime OccurredOnUtc, string PeriodId, string PeriodCode) : IDomainEvent;

public sealed record GradeRecorded(Guid EventId, DateTime OccurredOnUtc, string OfferingId, string StudentId, int Grade, string GradedByUserId) : IDomainEvent;
