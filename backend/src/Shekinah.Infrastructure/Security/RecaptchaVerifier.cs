using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shekinah.Application.Abstractions;

namespace Shekinah.Infrastructure.Security;

/// <summary>
/// Verificación de reCAPTCHA v3 contra el endpoint oficial de Google (siteverify). Cualquier fallo
/// de red/parseo se trata como "no verificado" (fail-closed): ante la duda, se rechaza la solicitud
/// en vez de dejar pasar un posible bot — el usuario legítimo simplemente reintenta.
/// </summary>
public sealed class RecaptchaVerifier(HttpClient httpClient, IOptions<RecaptchaSettings> options, ILogger<RecaptchaVerifier> logger) : IRecaptchaVerifier
{
    private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    public async Task<bool> VerifyAsync(string token, string expectedAction, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            // Sin SecretKey configurada (p. ej. ambiente local sin appsettings.Development.json)
            // no podemos verificar nada — se registra y se deja pasar para no bloquear desarrollo,
            // pero en producción esto SIEMPRE debe estar configurado.
            logger.LogWarning("Recaptcha:SecretKey no está configurada; se omite la verificación de reCAPTCHA.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = settings.SecretKey,
                ["response"] = token,
            });

            using var response = await httpClient.PostAsync(VerifyUrl, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("reCAPTCHA siteverify respondió {StatusCode}.", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken: ct);
            if (result is null || !result.Success)
            {
                logger.LogInformation("reCAPTCHA rechazado: success={Success}, errores={Errors}.",
                    result?.Success, result?.ErrorCodes is null ? "N/D" : string.Join(",", result.ErrorCodes));
                return false;
            }

            if (!string.Equals(result.Action, expectedAction, StringComparison.Ordinal))
            {
                logger.LogWarning("reCAPTCHA action inesperado: esperado={Expected}, recibido={Actual}.", expectedAction, result.Action);
                return false;
            }

            if (result.Score < settings.MinimumScore)
            {
                logger.LogInformation("reCAPTCHA score {Score} por debajo del umbral {Threshold}.", result.Score, settings.MinimumScore);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Fallo al verificar reCAPTCHA contra Google.");
            return false;
        }
    }

    private sealed class SiteVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("challenge_ts")]
        public string? ChallengeTs { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
