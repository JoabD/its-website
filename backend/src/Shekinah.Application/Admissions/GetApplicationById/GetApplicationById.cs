using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Admissions.GetApplicationById;

[RequireRole(UserRole.Administrator)]
public sealed record GetApplicationByIdQuery(string Id) : IQuery<ApplicationDetail>;

public sealed record ApplicationDetail(
    string Id, string Folio, string ApplicantName, string Email, string Phone, Modality Modality, string RegionName,
    string? OnlineReason, ApplicationStatus Status, DateTime SubmittedAtUtc, string? DecisionReason, DateTime? DecidedAtUtc);

public sealed class GetApplicationByIdQueryHandler(Domain.Admissions.IAdmissionApplicationRepository applications)
    : IQueryHandler<GetApplicationByIdQuery, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> HandleAsync(GetApplicationByIdQuery query, CancellationToken ct)
    {
        var application = await applications.GetByIdAsync(query.Id, ct);
        if (application is null)
        {
            return Result.Failure<ApplicationDetail>(Error.NotFound("AdmissionApplication.NotFound", "Solicitud no encontrada."));
        }

        return Result.Success(new ApplicationDetail(
            application.Id, application.Folio, application.Applicant.FullName.FullName, application.Applicant.Email.Value,
            application.Applicant.Phone.Value, application.ModalityChoice.Modality, application.ModalityChoice.RegionName,
            application.ModalityChoice.OnlineReason, application.Status, application.SubmittedAtUtc,
            application.Decision?.Reason, application.Decision?.DecidedAtUtc));
    }
}
