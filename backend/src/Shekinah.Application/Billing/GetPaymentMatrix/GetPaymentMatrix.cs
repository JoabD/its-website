using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Billing.GetPaymentMatrix;

/// <summary>
/// RN-09: si quien consulta es RegionalCoordinator, <see cref="IRegionScopeResolver"/> fuerza el
/// filtro por su región AQUÍ, en el handler — es inviolable desde el cliente (spec técnico §3.5:
/// read model detrás de un pipeline de agregación de Mongo, la lógica de negocio no vive ahí).
/// </summary>
[RequireRole(UserRole.Administrator, UserRole.RegionalCoordinator, UserRole.RegionalSecretary)]
public sealed record GetPaymentMatrixQuery(string PeriodId, string? RegionId, int Page, int PageSize) : IQuery<PagedResult<StudentPaymentRow>>;

public sealed class GetPaymentMatrixQueryHandler(IPaymentMatrixReader reader, IRegionScopeResolver regionScope)
    : IQueryHandler<GetPaymentMatrixQuery, PagedResult<StudentPaymentRow>>
{
    public async Task<Result<PagedResult<StudentPaymentRow>>> HandleAsync(GetPaymentMatrixQuery query, CancellationToken ct)
    {
        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();
        var effectiveRegionId = mandatoryRegionId ?? query.RegionId;

        var filter = new PaymentMatrixFilter(query.PeriodId, effectiveRegionId, query.Page, query.PageSize);
        var result = await reader.GetMatrixAsync(filter, ct);
        return Result.Success(result);
    }
}
