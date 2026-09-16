using Shekinah.Application.Abstractions;

namespace Shekinah.Infrastructure.Storage;

/// <summary>
/// Implementación mínima de IFileStorage sobre disco local (montado como volumen en docker-compose).
/// Sustituible por Azure Blob/S3 vía la misma interfaz (spec técnico §4-D) sin tocar Application.
/// </summary>
public sealed class LocalFileStorage(string rootPath) : IFileStorage
{
    public async Task<string> SaveAsync(string fileName, string contentType, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(rootPath);
        var fileId = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(rootPath, fileId);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);

        return fileId;
    }

    public Task<Stream> OpenAsync(string fileId, CancellationToken ct)
    {
        var fullPath = Path.Combine(rootPath, fileId);
        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }
}
