using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.GetOfferingEnrollments;

public sealed record GetOfferingEnrollmentsQuery(string OfferingId) : IQuery<IReadOnlyList<EnrollmentRow>>;

public sealed record EnrollmentRow(string StudentId, int EnrollmentNumber, string StudentName, int? Grade, string Status);

public sealed class GetOfferingEnrollmentsQueryHandler(Domain.Academics.ICourseOfferingRepository offerings, ICurrentUser currentUser)
    : IQueryHandler<GetOfferingEnrollmentsQuery, IReadOnlyList<EnrollmentRow>>
{
    public async Task<Result<IReadOnlyList<EnrollmentRow>>> HandleAsync(GetOfferingEnrollmentsQuery query, CancellationToken ct)
    {
        var offering = await offerings.GetByIdAsync(query.OfferingId, ct);
        if (offering is null)
        {
            return Result.Failure<IReadOnlyList<EnrollmentRow>>(Error.NotFound("CourseOffering.NotFound", "Oferta no encontrada."));
        }

        if (currentUser.Role == UserRole.Teacher && offering.Teacher.Id != currentUser.UserId)
        {
            return Result.Failure<IReadOnlyList<EnrollmentRow>>(Error.Forbidden("GetOfferingEnrollments.NotOwner", "Solo el profesor asignado o un administrador puede ver esta oferta."));
        }

        IReadOnlyList<EnrollmentRow> rows = offering.Enrollments
            .Select(e => new EnrollmentRow(e.StudentId, e.EnrollmentNumber, e.StudentName, e.Grade?.Value, e.Status.ToString()))
            .ToList();

        return Result.Success(rows);
    }
}
