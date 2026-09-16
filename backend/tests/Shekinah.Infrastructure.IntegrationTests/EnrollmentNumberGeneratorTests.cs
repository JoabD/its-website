using Microsoft.Extensions.Options;
using Shekinah.Infrastructure.Persistence;
using Shouldly;
using Testcontainers.MongoDb;
using Xunit;

namespace Shekinah.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica el generador de matrícula bajo concurrencia (findAndModify atómico, spec técnico Fase 3)
/// contra un MongoDB real levantado con Testcontainers.
/// </summary>
public class EnrollmentNumberGeneratorTests : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder().Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    [Fact(DisplayName = "Generador_de_matricula_es_atomico_bajo_concurrencia")]
    public async Task Generador_no_produce_duplicados_bajo_concurrencia()
    {
        var options = Options.Create(new MongoOptions { ConnectionString = _container.GetConnectionString(), DatabaseName = "shekinah_test" });
        var context = new MongoContext(options);
        var generator = new EnrollmentNumberGenerator(context);

        var tasks = Enumerable.Range(0, 50).Select(_ => generator.NextAsync(CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        results.Select(r => r.Value).Distinct().Count().ShouldBe(50);
    }
}
