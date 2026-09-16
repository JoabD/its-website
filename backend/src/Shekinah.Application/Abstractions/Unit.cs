namespace Shekinah.Application.Abstractions;

/// <summary>Marcador de "sin valor de retorno" para comandos que solo importan por su efecto (Result&lt;Unit&gt;).</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
