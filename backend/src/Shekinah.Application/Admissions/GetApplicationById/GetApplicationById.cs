using Shekinah.Application.Abstractions;
using Shekinah.Domain.Admissions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Admissions.GetApplicationById;

[RequireRole(UserRole.Administrator)]
public sealed record GetApplicationByIdQuery(string Id) : IQuery<ApplicationDetail>;

public sealed record ChecklistItemState(string Id, string Label, bool Checked);

public sealed record ApplicationChecklistDetail(IReadOnlyList<ChecklistItemState> Items, bool IsComplete);

/// <summary>Ficha completa para el panel de revisión del admin — todos los campos capturados en el
/// wizard público (a diferencia de la versión resumida que usa la bandeja de la lista).</summary>
public sealed record ApplicationDetail(
    string Id, string Folio, string FullName, DateOnly BirthDate, string MaritalStatus, string Email, string Phone,
    string Street, string Neighborhood, string Locality, string Municipality, string? State,
    string ChurchName, string ChurchStreet, string ChurchNeighborhood, string ChurchLocality, string ChurchMunicipality,
    string PastorName, string TimeAttending, bool HasMinistryRole, string? MinistryRoleName,
    string EducationLevel, string? OtherEducationDescription, string TheologicalBackground, string StudyPurpose,
    Modality Modality, string RegionName, string? OnlineReason,
    ApplicationStatus Status, DateTime SubmittedAtUtc, string? DecisionReason, DateTime? DecidedAtUtc,
    bool IsDeletable, bool ApprovedViaQuickAction, DateTime? PurgeScheduledAtUtc, ApplicationChecklistDetail Checklist);

public sealed class GetApplicationByIdQueryHandler(IAdmissionApplicationRepository applications, IChecklistItemDefinitionRepository checklistItems)
    : IQueryHandler<GetApplicationByIdQuery, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> HandleAsync(GetApplicationByIdQuery query, CancellationToken ct)
    {
        var application = await applications.GetByIdAsync(query.Id, ct);
        if (application is null)
        {
            return Result.Failure<ApplicationDetail>(Error.NotFound("AdmissionApplication.NotFound", "Solicitud no encontrada."));
        }

        var applicant = application.Applicant;
        var church = applicant.Church;

        var activeItems = await checklistItems.GetActiveAsync(ct);
        var checklistStates = activeItems
            .Select(i => new ChecklistItemState(i.Id, i.Label, application.CheckedChecklistItemIds.Contains(i.Id)))
            .ToList();
        var isComplete = application.IsChecklistCompleteFor(activeItems.Select(i => i.Id).ToList());

        return Result.Success(new ApplicationDetail(
            application.Id, application.Folio, applicant.FullName.FullName, applicant.BirthDate, applicant.MaritalStatus,
            applicant.Email.Value, applicant.Phone.Value,
            applicant.Address.Street, applicant.Address.Neighborhood, applicant.Address.Locality, applicant.Address.Municipality, applicant.Address.State,
            church.Name, church.Address.Street, church.Address.Neighborhood, church.Address.Locality, church.Address.Municipality,
            church.PastorName, church.TimeAttending, church.MinistryRole.HasRole, church.MinistryRole.RoleName,
            applicant.Education.Level.ToString(), applicant.Education.OtherDescription, applicant.TheologicalBackground, applicant.StudyPurpose,
            application.ModalityChoice.Modality, application.ModalityChoice.RegionName, application.ModalityChoice.OnlineReason,
            application.Status, application.SubmittedAtUtc, application.Decision?.Reason, application.Decision?.DecidedAtUtc,
            application.IsDeletable, application.ApprovedViaQuickAction, application.PurgeScheduledAtUtc,
            new ApplicationChecklistDetail(checklistStates, isComplete)));
    }
}
