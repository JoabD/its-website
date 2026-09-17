using Shekinah.Application.Abstractions;
using Shekinah.Domain.Announcements;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Announcements.GetAnnouncements;

/// <summary>
/// Sin <c>[RequireRole]</c> a propósito: cualquier persona autenticada puede consultar los avisos
/// institucionales (alumno, docente, staff) — solo publicarlos es exclusivo de cuentas maestras
/// (ver CreateAnnouncementCommand). El pipeline de <c>AuthorizationBehavior</c> igual exige sesión
/// válida (no es [AllowAnonymousUseCase]).
/// </summary>
public sealed record GetAnnouncementsQuery(int Limit) : IQuery<IReadOnlyList<AnnouncementListItem>>;

public sealed record AnnouncementListItem(string Id, string Title, string Body, DateTime PublishedAtUtc);

public sealed class GetAnnouncementsQueryHandler(IAnnouncementRepository announcements)
    : IQueryHandler<GetAnnouncementsQuery, IReadOnlyList<AnnouncementListItem>>
{
    public async Task<Result<IReadOnlyList<AnnouncementListItem>>> HandleAsync(GetAnnouncementsQuery query, CancellationToken ct)
    {
        var limit = query.Limit <= 0 ? 20 : query.Limit;
        var items = await announcements.GetRecentAsync(limit, ct);

        IReadOnlyList<AnnouncementListItem> mapped = items
            .Select(a => new AnnouncementListItem(a.Id, a.Title, a.Body, a.PublishedAtUtc))
            .ToList();

        return Result.Success(mapped);
    }
}
