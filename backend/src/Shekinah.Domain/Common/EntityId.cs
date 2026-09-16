using System.Security.Cryptography;

namespace Shekinah.Domain.Common;

/// <summary>
/// Genera identificadores de entidad como cadenas hexadecimales de 24 dígitos, el mismo formato que
/// <c>MongoDB.Bson.ObjectId</c>, sin que Domain ni Application dependan del paquete MongoDB.Bson
/// (R3: Domain sin NuGet). Infrastructure hace <c>ObjectId.Parse(id)</c> al persistir (ver
/// Persistence/Repositories/*), así que todo Id nuevo debe generarse con <see cref="NewId"/> — nunca
/// con <c>Guid.NewGuid().ToString()</c>, que produce un formato con guiones que ObjectId.Parse rechaza.
/// </summary>
public static class EntityId
{
    public static string NewId()
    {
        Span<byte> bytes = stackalloc byte[12];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
