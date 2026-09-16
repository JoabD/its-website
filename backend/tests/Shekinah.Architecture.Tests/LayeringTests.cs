using System.Reflection;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Shekinah.Architecture.Tests;

/// <summary>
/// R3: "Prohibida la dependencia invertida... Esto se verifica con tests de arquitectura
/// automatizados (NetArchTest), no con buena voluntad." Lista exacta de ESPECIFICACION-TECNICA.md §4.
/// </summary>
public class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(Shekinah.Domain.Common.Result).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Shekinah.Application.Abstractions.IDispatcher).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Shekinah.Infrastructure.Persistence.MongoContext).Assembly;

    [Fact]
    public void Domain_should_not_reference_any_other_project()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOnAny("Shekinah.Application", "Shekinah.Infrastructure", "Shekinah.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_project_should_have_zero_nuget_references()
    {
        var referencedAssemblies = DomainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        // Solo se permiten ensamblados del propio .NET (System.*, netstandard, mscorlib).
        var nonFrameworkReferences = referencedAssemblies.Where(name =>
            name is not null && !name.StartsWith("System") && name != "netstandard" && name != "mscorlib").ToList();

        nonFrameworkReferences.ShouldBeEmpty();
    }

    [Fact]
    public void Application_should_not_reference_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should().NotHaveDependencyOnAny("Shekinah.Infrastructure", "Shekinah.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_types_should_not_have_Mongo_attributes()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOn("MongoDB.Bson")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Aggregates_should_not_expose_public_setters()
    {
        var aggregateBase = typeof(Shekinah.Domain.Common.AggregateRoot<>);

        var offenders = Types.InAssembly(DomainAssembly)
            .That().Inherit(aggregateBase)
            .GetTypes()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.GetSetMethod(nonPublic: false) is not null)
            .Select(p => $"{p.DeclaringType?.Name}.{p.Name}")
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void Handlers_should_be_sealed()
    {
        var commandHandlerInterface = typeof(Shekinah.Application.Abstractions.ICommandHandler<,>);
        var queryHandlerInterface = typeof(Shekinah.Application.Abstractions.IQueryHandler<,>);

        var offenders = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsSealed)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == commandHandlerInterface || i.GetGenericTypeDefinition() == queryHandlerInterface)))
            .Select(t => t.FullName)
            .ToList();

        offenders.ShouldBeEmpty();
    }
}
