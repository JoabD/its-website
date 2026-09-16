using Microsoft.Extensions.Options;
using Shekinah.Application.Abstractions;

namespace Shekinah.Infrastructure.Outbox;

/// <summary>Implementación de <see cref="INotificationRecipients"/> sobre <see cref="EmailSettings"/>.</summary>
public sealed class EmailSettingsNotificationRecipients(IOptions<EmailSettings> options) : INotificationRecipients
{
    public string? AdministrativeCc => options.Value.DefaultCcAddress;
}
