using NSubstitute;
using Shekinah.Application.Identity.Login;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;
using Shouldly;
using Xunit;

namespace Shekinah.Application.UnitTests.Identity;

/// <summary>
/// RN-07 probado con dobles (NSubstitute), cero acceso a Mongo (criterio de aceptación Fase 2).
/// Representativo del patrón a seguir para el resto de los handlers.
/// </summary>
public class LoginCommandHandlerTests
{
    [Fact(DisplayName = "RN_07_Login_con_matricula_inexistente_no_revela_si_existe")]
    public async Task RN_07_Matricula_inexistente_no_revela_informacion()
    {
        var users = Substitute.For<IUserRepository>();
        users.GetByEnrollmentNumberAsync(Arg.Any<EnrollmentNumber>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var handler = new LoginCommandHandler(users, Substitute.For<Abstractions.IPasswordHasher>(), Substitute.For<Abstractions.ITokenService>(), new FixedClock());

        var result = await handler.HandleAsync(new LoginCommand(9999, "cualquiera"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Auth.InvalidCredentials");
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
