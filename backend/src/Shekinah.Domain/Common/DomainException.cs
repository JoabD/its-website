namespace Shekinah.Domain.Common;

/// <summary>
/// Excepción reservada para invariantes rotos por un error de programación (por ejemplo, invocar
/// un método de comportamiento con precondiciones violadas que el propio código de dominio debió
/// impedir). Las reglas de negocio "esperables" NUNCA usan esto: usan <see cref="Result"/>.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
