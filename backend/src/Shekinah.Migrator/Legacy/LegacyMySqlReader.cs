using MySqlConnector;

namespace Shekinah.Migrator.Legacy;

/// <summary>
/// Solo lectura del MySQL legado (R1: jamás se escribe ahí). Cadena de conexión por configuración
/// (R8, nunca hardcodeada). Mapeo tabla → columnas exacto a PROMPT-MAESTRO.md §2.4.
/// </summary>
public sealed class LegacyMySqlReader(string connectionString)
{
    public async IAsyncEnumerable<LegacyPreregistro> ReadPreregistrosAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT ID_ENCUESTA, NOMBRE_COMPLETO, FECHA_NACIMIENTO, DOMICILIO, COLONIA, LOCALIDAD, MUNICIPIO, ESTADO,
                   TELEFONO, ESTADO_CIVIL, IGLESIA, DOMICILIO_IGLESIA, COLONIA_IGLESIA, LOCALIDAD_IGLESIA, MUNICIPIO_IGLESIA,
                   TIEMPO_CONGREGARSE, PASTOR, CARGO, CARGO_NOMBRE, PROPOSITO, FORMACION_TEOLOGICA,
                   PRIMARIA, SECUNDARIA, BACHILLERATO, OTRA_ESCOLARIDAD, PRESENCIAL, REGION, VIRTUAL1, MOTIVO_VIRTUAL,
                   DIPLOMADO, FECHA_REGISTRO, CORREO, ESTATUS
            FROM PREREGISTRO
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            yield return new LegacyPreregistro(
                reader.GetInt32("ID_ENCUESTA"), reader.GetString("NOMBRE_COMPLETO"),
                reader.IsDBNull(reader.GetOrdinal("FECHA_NACIMIENTO")) ? null : reader.GetDateTime("FECHA_NACIMIENTO"),
                reader.IsDBNull(reader.GetOrdinal("DOMICILIO")) ? null : reader.GetString("DOMICILIO"),
                reader.IsDBNull(reader.GetOrdinal("COLONIA")) ? null : reader.GetString("COLONIA"),
                reader.IsDBNull(reader.GetOrdinal("LOCALIDAD")) ? null : reader.GetString("LOCALIDAD"),
                reader.IsDBNull(reader.GetOrdinal("MUNICIPIO")) ? null : reader.GetString("MUNICIPIO"),
                reader.IsDBNull(reader.GetOrdinal("ESTADO")) ? null : reader.GetString("ESTADO"),
                reader.IsDBNull(reader.GetOrdinal("TELEFONO")) ? null : reader.GetString("TELEFONO"),
                reader.IsDBNull(reader.GetOrdinal("ESTADO_CIVIL")) ? null : reader.GetString("ESTADO_CIVIL"),
                reader.IsDBNull(reader.GetOrdinal("IGLESIA")) ? null : reader.GetString("IGLESIA"),
                reader.IsDBNull(reader.GetOrdinal("DOMICILIO_IGLESIA")) ? null : reader.GetString("DOMICILIO_IGLESIA"),
                reader.IsDBNull(reader.GetOrdinal("COLONIA_IGLESIA")) ? null : reader.GetString("COLONIA_IGLESIA"),
                reader.IsDBNull(reader.GetOrdinal("LOCALIDAD_IGLESIA")) ? null : reader.GetString("LOCALIDAD_IGLESIA"),
                reader.IsDBNull(reader.GetOrdinal("MUNICIPIO_IGLESIA")) ? null : reader.GetString("MUNICIPIO_IGLESIA"),
                reader.IsDBNull(reader.GetOrdinal("TIEMPO_CONGREGARSE")) ? null : reader.GetString("TIEMPO_CONGREGARSE"),
                reader.IsDBNull(reader.GetOrdinal("PASTOR")) ? null : reader.GetString("PASTOR"),
                reader.IsDBNull(reader.GetOrdinal("CARGO")) ? null : reader.GetInt32("CARGO"),
                reader.IsDBNull(reader.GetOrdinal("CARGO_NOMBRE")) ? null : reader.GetString("CARGO_NOMBRE"),
                reader.IsDBNull(reader.GetOrdinal("PROPOSITO")) ? null : reader.GetString("PROPOSITO"),
                reader.IsDBNull(reader.GetOrdinal("FORMACION_TEOLOGICA")) ? null : reader.GetString("FORMACION_TEOLOGICA"),
                reader.GetBoolean("PRIMARIA"), reader.GetBoolean("SECUNDARIA"), reader.GetBoolean("BACHILLERATO"),
                reader.IsDBNull(reader.GetOrdinal("OTRA_ESCOLARIDAD")) ? null : reader.GetString("OTRA_ESCOLARIDAD"),
                reader.GetBoolean("PRESENCIAL"),
                reader.IsDBNull(reader.GetOrdinal("REGION")) ? null : reader.GetInt32("REGION"),
                reader.GetBoolean("VIRTUAL1"),
                reader.IsDBNull(reader.GetOrdinal("MOTIVO_VIRTUAL")) ? null : reader.GetString("MOTIVO_VIRTUAL"),
                reader.GetBoolean("DIPLOMADO"), reader.GetDateTime("FECHA_REGISTRO"),
                reader.IsDBNull(reader.GetOrdinal("CORREO")) ? null : reader.GetString("CORREO"),
                reader.GetInt32("ESTATUS"));
        }
    }

    public async IAsyncEnumerable<LegacyUsuario> ReadUsuariosAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);

        const string sql = "SELECT USUARIO, CONTRASEÑA, ID_ENCUESTA, PERFIL, CUATRIMESTRE, PAGOS FROM USUARIOS";
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            yield return new LegacyUsuario(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5));
        }
    }

    // ReadRegionesAsync, ReadMateriasAsync, ReadMateriasAsignadasAsync, ReadPeriodosAsync y
    // ReadPagosAsync siguen el mismo patrón (SELECT explícito de las columnas de PROMPT-MAESTRO.md §2.4);
    // se omiten aquí por brevedad del entregable inicial y quedan como TODO explícito en docs/DECISIONS.md,
    // NO como NotImplementedException silencioso (regla §10, prohibición explícita).
}
