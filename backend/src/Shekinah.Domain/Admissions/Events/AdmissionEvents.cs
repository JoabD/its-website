using Shekinah.Domain.Common;

namespace Shekinah.Domain.Admissions.Events;

public sealed record AdmissionApplicationSubmitted(Guid EventId, DateTime OccurredOnUtc, string ApplicationId, string Folio) : IDomainEvent;

public sealed record AdmissionApplicationApproved(
    Guid EventId, DateTime OccurredOnUtc, string ApplicationId, string DecidedByUserId, string DecidedByName) : IDomainEvent;

public sealed record AdmissionApplicationRejected(
    Guid EventId, DateTime OccurredOnUtc, string ApplicationId, string DecidedByUserId, string Reason) : IDomainEvent;
