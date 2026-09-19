using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Shekinah.Infrastructure.Persistence;

/// <summary>
/// Registro central de BsonClassMap (spec técnico §3.3-D): "Ningún atributo [BsonId]/[BsonElement]
/// en las clases de dominio." El dominio no sabe que existe Mongo; todo el mapeo vive aquí, en
/// Infrastructure. Convenciones camelCase, DateTime en UTC, decimal como Decimal128.
/// </summary>
public sealed class MongoContext
{
    private static bool _classMapsRegistered;
    private static readonly object Lock = new();

    public MongoContext(IOptions<MongoOptions> options)
    {
        RegisterConventions();
        RegisterClassMaps();

        var client = new MongoClient(options.Value.ConnectionString);
        Client = client;
        Database = client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<BsonDocument> Regions => Database.GetCollection<BsonDocument>("regions");

    public IMongoCollection<BsonDocument> Subjects => Database.GetCollection<BsonDocument>("subjects");

    public IMongoCollection<BsonDocument> AcademicPeriods => Database.GetCollection<BsonDocument>("academicPeriods");

    public IMongoCollection<BsonDocument> Users => Database.GetCollection<BsonDocument>("users");

    public IMongoCollection<BsonDocument> AdmissionApplications => Database.GetCollection<BsonDocument>("admissionApplications");

    public IMongoCollection<BsonDocument> CourseOfferings => Database.GetCollection<BsonDocument>("courseOfferings");

    public IMongoCollection<BsonDocument> Payments => Database.GetCollection<BsonDocument>("payments");

    public IMongoCollection<BsonDocument> PaymentImportBatches => Database.GetCollection<BsonDocument>("paymentImportBatches");

    public IMongoCollection<BsonDocument> PaymentNotices => Database.GetCollection<BsonDocument>("paymentNotices");

    public IMongoCollection<BsonDocument> OutboxMessages => Database.GetCollection<BsonDocument>("outboxMessages");

    public IMongoCollection<BsonDocument> Counters => Database.GetCollection<BsonDocument>("counters");

    public IMongoCollection<BsonDocument> RefreshTokens => Database.GetCollection<BsonDocument>("refreshTokens");

    public IMongoCollection<BsonDocument> AuditLogs => Database.GetCollection<BsonDocument>("auditLogs");

    public IMongoCollection<BsonDocument> Migrations => Database.GetCollection<BsonDocument>("_migrations");

    public IMongoCollection<BsonDocument> Announcements => Database.GetCollection<BsonDocument>("announcements");

    public IMongoCollection<BsonDocument> CalendarEvents => Database.GetCollection<BsonDocument>("calendarEvents");

    public IMongoCollection<BsonDocument> ChecklistItemDefinitions => Database.GetCollection<BsonDocument>("checklistItemDefinitions");

    private static void RegisterConventions()
    {
        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
            new EnumRepresentationConvention(BsonType.String), // estados como string legible, nunca números mágicos (spec técnico §5.1)
        };
        ConventionRegistry.Register("shekinah-conventions", pack, _ => true);
    }

    /// <summary>
    /// Nota de diseño: en lugar de mapear cada agregado 1:1 con BsonClassMap (lo que exigiría
    /// constructores públicos/setters que el dominio prohíbe deliberadamente — sin setters
    /// públicos, factory methods privados), los repositorios de este proyecto serializan/deserializan
    /// vía BsonDocument + Value Objects propios (ToBsonDocument/FromBsonDocument explícitos en cada
    /// repositorio, ver Persistence/Repositories/*). Aquí solo se registran los serializadores de
    /// primitivos compartidos (DateTime→UTC, decimal→Decimal128) que sí aplican de forma global.
    /// </summary>
    private static void RegisterClassMaps()
    {
        lock (Lock)
        {
            if (_classMapsRegistered) return;

            BsonSerializer.RegisterSerializer(typeof(DateTime), DateTimeSerializer.UtcInstance);
            BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
            BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

            _classMapsRegistered = true;
        }
    }
}
