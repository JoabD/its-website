using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Billing.ImportPayments;
using Shekinah.Application.Identity.RefreshToken;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Admissions;
using Shekinah.Domain.Announcements;
using Shekinah.Domain.Billing;
using Shekinah.Domain.Calendar;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Infrastructure.Admissions;
using Shekinah.Infrastructure.Auth;
using Shekinah.Infrastructure.Documents;
using Shekinah.Infrastructure.Outbox;
using Shekinah.Infrastructure.Persistence;
using Shekinah.Infrastructure.Persistence.Migrations;
using Shekinah.Infrastructure.Persistence.ReadModels;
using Shekinah.Infrastructure.Persistence.Repositories;
using Shekinah.Infrastructure.Security;
using Shekinah.Infrastructure.Seeding;
using Shekinah.Infrastructure.Spreadsheets;
using Shekinah.Infrastructure.Storage;
using Shekinah.Infrastructure.Time;

namespace Shekinah.Infrastructure;

/// <summary>Composition de Infrastructure, invocada desde Shekinah.Api (composition root, spec técnico §4-D).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<RecaptchaSettings>(configuration.GetSection(RecaptchaSettings.SectionName));

        services.AddSingleton<MongoContext>();
        services.AddSingleton(sp => sp.GetRequiredService<MongoContext>().Client);
        services.AddSingleton(sp => sp.GetRequiredService<MongoContext>().Database);
        services.AddHttpContextAccessor();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IRegionScopeResolver, RegionScopeResolver>();
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
        services.AddScoped<IEmailSender, OutboxEmailSender>();
        services.AddSingleton<INotificationRecipients, EmailSettingsNotificationRecipients>();
        services.AddScoped<IRefreshTokenStore, MongoRefreshTokenStore>();
        services.AddScoped<IEnrollmentNumberGenerator, EnrollmentNumberGenerator>();
        services.AddScoped<IMatriculaGenerator, MatriculaGenerator>();
        services.AddSingleton<ISpreadsheetReader, SpreadsheetReader>();
        services.AddScoped<IPaymentMatrixReader, PaymentMatrixReader>();
        services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(configuration["Storage:RootPath"] ?? "/data/uploads"));
        services.AddSingleton<IKardexPdfGenerator, KardexPdfGenerator>();
        services.AddSingleton<IAdmissionFichaPdfGenerator, AdmissionFichaPdfGenerator>();
        services.AddSingleton<IPaymentReceiptPdfGenerator, PaymentReceiptPdfGenerator>();
        services.AddHttpClient<IRecaptchaVerifier, RecaptchaVerifier>();

        services.AddScoped<IRegionRepository, RegionRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAdmissionApplicationRepository, AdmissionApplicationRepository>();
        services.AddScoped<IChecklistItemDefinitionRepository, ChecklistItemDefinitionRepository>();
        services.AddScoped<IAcademicPeriodRepository, AcademicPeriodRepository>();
        services.AddScoped<ICourseOfferingRepository, CourseOfferingRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentNoticeRepository, PaymentNoticeRepository>();
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();

        services.AddSingleton<IMongoMigration, M001_CreateIndexesAndValidators>();
        services.AddSingleton<IMongoMigration, M002_RenameOnsiteRegions>();
        services.AddSingleton<IMongoMigration, M003_AddRegionAbbreviations>();
        services.AddSingleton<IMongoMigration, M004_SeedChecklistItemDefinitions>();
        services.AddScoped<MigrationRunner>();
        services.AddScoped<DatabaseSeeder>();

        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<AdmissionApplicationPurgeJob>();

        return services;
    }
}
