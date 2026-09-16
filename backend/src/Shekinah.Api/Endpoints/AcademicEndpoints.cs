using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Academics.AssignTeacher;
using Shekinah.Application.Academics.AutoEnroll;
using Shekinah.Application.Academics.GetCurrentPeriod;
using Shekinah.Application.Academics.GetOfferingEnrollments;
using Shekinah.Application.Academics.GetOfferings;
using Shekinah.Application.Academics.GetPeriods;
using Shekinah.Application.Academics.OpenPeriod;
using Shekinah.Application.Academics.RecordGrade;

namespace Shekinah.Api.Endpoints;

public static class AcademicEndpoints
{
    public static void MapAcademicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/academic").WithTags("Académico").RequireAuthorization();

        group.MapGet("/periods", async (int page, int pageSize, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetPeriodsQuery(page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct)).ToApiResult());

        group.MapGet("/periods/current", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetCurrentPeriodQuery(), ct)).ToApiResult());

        group.MapPost("/periods", async (OpenPeriodCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created));

        group.MapGet("/offerings", async (string periodId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetOfferingsQuery(periodId), ct)).ToApiResult());

        group.MapPost("/offerings", async (AssignTeacherCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created));

        group.MapDelete("/offerings/{id}", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RemoveOfferingCommand(id), ct)).ToApiResult());

        group.MapPost("/offerings/auto-enroll", async (AutoEnrollRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new AutoEnrollCommand(request.PeriodId), ct)).ToApiResult());

        group.MapGet("/offerings/{id}/enrollments", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetOfferingEnrollmentsQuery(id), ct)).ToApiResult());

        group.MapPut("/offerings/{id}/enrollments/{studentId}/grade", async (string id, string studentId, GradeRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RecordGradeCommand(id, studentId, request.Grade), ct)).ToApiResult());
    }

    public sealed record AutoEnrollRequest(string PeriodId);

    public sealed record GradeRequest(int Grade);
}
