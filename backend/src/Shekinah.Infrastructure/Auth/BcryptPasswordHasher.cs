using Shekinah.Application.Abstractions;

namespace Shekinah.Infrastructure.Auth;

/// <summary>
/// Compatible con los hashes $2y$ del legado (PHP password_hash), verificado en Fase 8 con un test
/// que confirma que un hash migrado sigue validando su contraseña original.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plainPassword) => BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);

    public bool Verify(string plainPassword, string hash) => BCrypt.Net.BCrypt.Verify(plainPassword, hash);
}
