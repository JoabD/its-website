namespace Shekinah.Domain.Common;

/// <summary>
/// Tipo de error de dominio/aplicación. Se traduce 1:1 a un código HTTP en la capa Api
/// (ver ESPECIFICACION-TECNICA.md §3.6): Validation-&gt;400, Unauthorized-&gt;401, Forbidden-&gt;403,
/// NotFound-&gt;404, Conflict-&gt;409, Failure-&gt;500.
/// </summary>
public enum ErrorType
{
    None = 0,
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
    Failure,
}

/// <summary>
/// Error de negocio tipado. Nunca se expone <c>exception.Message</c> al cliente (corrige el
/// <c>dd($e-&gt;getMessage())</c> del legado, ver PROMPT-MAESTRO.md §2.5.3).
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}

/// <summary>
/// Resultado de una operación que puede fallar de forma esperada. "Regla de negocio incumplida ⇒
/// Result&lt;T&gt; con error tipado. Excepción ⇒ solo para lo imprevisto" (spec técnico §1).
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("Un resultado exitoso no puede tener un error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("Un resultado fallido debe tener un error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>Resultado con valor de retorno en el camino feliz.</summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Lanza si se accede en un resultado fallido: acceder al valor de un fallo es un error de
    /// programación, no un caso de negocio esperado.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"No se puede acceder a Value de un resultado fallido. Error: {Error.Code}");

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);
}
