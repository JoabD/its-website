using Shekinah.Domain.Common;

namespace Shekinah.Domain.Announcements;

/// <summary>
/// Aviso institucional (plan de control escolar, fase 6). Distinto por completo de
/// <c>Shekinah.Domain.Billing.PaymentNotice</c> (aviso de morosidad) — este es contenido
/// informativo que publican las cuentas maestras (Administrator), nunca el sistema automáticamente.
/// Exclusivo de cuentas maestras para publicar (confirmado por el usuario); cualquier persona
/// autenticada puede consultarlos (no requiere <c>[RequireRole]</c> en el query).
/// </summary>
public sealed class Announcement : AggregateRoot<string>
{
    private Announcement() { }

    private Announcement(string id, string title, string body, string publishedByUserId, IClock clock)
        : base(id)
    {
        Title = title;
        Body = body;
        PublishedByUserId = publishedByUserId;
        PublishedAtUtc = clock.UtcNow;
    }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string PublishedByUserId { get; private set; } = string.Empty;

    public DateTime PublishedAtUtc { get; private set; }

    /// <summary>Se envió por correo a los docentes activos al publicarse (fase 6: confirmado por
    /// el usuario — "deben llegar por correo a los correos de cada docente"). Queda registrado
    /// para no reintentar el envío si el aviso se vuelve a leer/editar más adelante.</summary>
    public bool EmailedToTeachers { get; private set; }

    public static Result<Announcement> Publish(string id, string title, string body, string publishedByUserId, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<Announcement>(Error.Validation("Announcement.TitleRequired", "El título del aviso es requerido."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure<Announcement>(Error.Validation("Announcement.BodyRequired", "El contenido del aviso es requerido."));
        }

        return Result.Success(new Announcement(id, title.Trim(), body.Trim(), publishedByUserId, clock));
    }

    public static Announcement Rehydrate(string id, string title, string body, string publishedByUserId, DateTime publishedAtUtc, bool emailedToTeachers) => new()
    {
        Id = id,
        Title = title,
        Body = body,
        PublishedByUserId = publishedByUserId,
        PublishedAtUtc = publishedAtUtc,
        EmailedToTeachers = emailedToTeachers,
    };

    public void MarkEmailedToTeachers() => EmailedToTeachers = true;
}

public interface IAnnouncementRepository
{
    Task AddAsync(Announcement announcement, CancellationToken ct);

    Task<IReadOnlyList<Announcement>> GetRecentAsync(int limit, CancellationToken ct);
}
