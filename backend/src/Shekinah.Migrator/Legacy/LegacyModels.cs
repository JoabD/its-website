namespace Shekinah.Migrator.Legacy;

/// <summary>
/// Modelos POCO que reflejan literalmente el esquema inferido en PROMPT-MAESTRO.md §2.4 (leído de
/// los Modelos Eloquent y las queries de los 10 controladores del legado). Solo se usan durante la
/// migración; nunca se referencian desde Domain ni Application.
/// </summary>
public sealed record LegacyPreregistro(
    int IdEncuesta, string NombreCompleto, DateTime? FechaNacimiento, string? Domicilio, string? Colonia,
    string? Localidad, string? Municipio, string? Estado, string? Telefono, string? EstadoCivil,
    string? Iglesia, string? DomicilioIglesia, string? ColoniaIglesia, string? LocalidadIglesia, string? MunicipioIglesia,
    string? TiempoCongregarse, string? Pastor, int? Cargo, string? CargoNombre, string? Proposito,
    string? FormacionTeologica, bool Primaria, bool Secundaria, bool Bachillerato, string? OtraEscolaridad,
    bool Presencial, int? Region, bool Virtual1, string? MotivoVirtual, bool Diplomado,
    DateTime FechaRegistro, string? Correo, int Estatus);

public sealed record LegacyUsuario(int Usuario, string Contrasena, int IdEncuesta, int Perfil, int Cuatrimestre, int Pagos);

public sealed record LegacyRegion(int IdRegion, string Nombre);

public sealed record LegacyMateria(int IdMateria, string NombreMateria, int Cuatrimestre);

public sealed record LegacyMateriaAsignada(int Id, int Materia, int Profesor, int Periodo, int Region, int? Alumno, int? Calificacion);

public sealed record LegacyPeriodo(int IdPeriodo, int Periodo, string Descripcion, DateTime FechaInicial, DateTime FechaFinal, int Estatus);

public sealed record LegacyPago(int Usuario, string Periodo);
