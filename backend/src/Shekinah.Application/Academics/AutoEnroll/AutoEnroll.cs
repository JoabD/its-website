using Shekinah.Application.Abstractions;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.AutoEnroll;

/// <summary>RN-15. Reconstruida del contrato observable de insertar_materias_alumnos() (ver PROMPT-MAESTRO.md §11).</summary>
[RequireRole(UserRole.Administrator)]
public sealed record AutoEnrollCommand(string PeriodId) : ICommand<AutoEnrollResponse>;

public sealed record AutoEnrollResponse(int OfferingsProcessed, int EnrollmentsCreated);

public sealed class AutoEnrollCommandHandler(ICourseOfferingRepository offerings, IUserRepository users, IClock clock)
    : ICommandHandler<AutoEnrollCommand, AutoEnrollResponse>
{
    public async Task<Result<AutoEnrollResponse>> HandleAsync(AutoEnrollCommand command, CancellationToken ct)
    {
        var periodOfferings = await offerings.GetByPeriodAsync(command.PeriodId, ct);
        var enrollmentsCreated = 0;

        foreach (var offering in periodOfferings)
        {
            // Universo candidato: alumnos activos de la región de la oferta (RN-15).
            var candidates = await users.GetActiveStudentsInRegionAsync(offering.Region.Id, currentTerm: null, ct);

            // 2026-10 ("promover alumnos"): esta matriculación ahora vive en CourseEnrollmentService,
            // compartida con PromoteStudentsCommandHandler — mismo comportamiento de antes, solo que
            // ya no está duplicado.
            var enrollResult = CourseEnrollmentService.EnrollEligible(offering, candidates, clock);
            if (enrollResult.IsFailure)
            {
                return Result.Failure<AutoEnrollResponse>(enrollResult.Error);
            }

            enrollmentsCreated += enrollResult.Value;
            await offerings.UpdateAsync(offering, ct);
        }

        return Result.Success(new AutoEnrollResponse(periodOfferings.Count, enrollmentsCreated));
    }
}
