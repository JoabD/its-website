using Shekinah.Application.Abstractions;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Catalog.ManageSubjects;

/// <summary>
/// "Catálogo de materias" (2026-10, pedido explícito del cliente: "el admin debe también poder
/// agregar una materia nueva al plan que sea y del número de cuatrimestre o semestre que sea").
/// <paramref name="Plan"/> es el hueco que tenía este comando: existía desde antes pero SIEMPRE
/// creaba la materia en <see cref="StudyPlan.Quarterly"/> porque nunca se le pasaba a
/// <see cref="Subject.CreateQuarterly"/> — así que era imposible dar de alta una materia del plan
/// Semestral. Ignorado cuando <see cref="ProgramType"/> es Diploma (los diplomados siempre son
/// Cuatrimestral, ver <see cref="Subject.CreateDiploma"/>).
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record CreateSubjectCommand(string Code, string Name, ProgramType ProgramType, int? TermNumber, int DisplayOrder, StudyPlan Plan = StudyPlan.Quarterly) : ICommand<string>;

public sealed class CreateSubjectCommandHandler(ISubjectRepository subjects) : ICommandHandler<CreateSubjectCommand, string>
{
    public async Task<Result<string>> HandleAsync(CreateSubjectCommand command, CancellationToken ct)
    {
        Result<Subject> result;

        if (command.ProgramType == ProgramType.Diploma)
        {
            result = Subject.CreateDiploma(EntityId.NewId(), command.Code, command.Name, command.DisplayOrder);
        }
        else
        {
            var termResult = TermNumber.Create(command.TermNumber ?? 0);
            if (termResult.IsFailure) return Result.Failure<string>(termResult.Error);
            result = Subject.CreateQuarterly(EntityId.NewId(), command.Code, command.Name, termResult.Value, command.DisplayOrder, plan: command.Plan);
        }

        if (result.IsFailure) return Result.Failure<string>(result.Error);

        await subjects.AddAsync(result.Value, ct);
        return Result.Success(result.Value.Id);
    }
}

[RequireRole(UserRole.Administrator)]
public sealed record PublishSyllabusCommand(string SubjectId, string PdfUrl) : ICommand<Abstractions.Unit>;

public sealed class PublishSyllabusCommandHandler(ISubjectRepository subjects, IClock clock) : ICommandHandler<PublishSyllabusCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(PublishSyllabusCommand command, CancellationToken ct)
    {
        var subject = await subjects.GetByIdAsync(command.SubjectId, ct);
        if (subject is null) return Result.Failure<Abstractions.Unit>(Error.NotFound("Subject.NotFound", "Materia no encontrada."));

        subject.PublishSyllabus(command.PdfUrl, clock);
        await subjects.UpdateAsync(subject, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}

[RequireRole(UserRole.Administrator)]
public sealed record DeleteSubjectCommand(string SubjectId) : ICommand<Abstractions.Unit>;

public sealed class DeleteSubjectCommandHandler(ISubjectRepository subjects) : ICommandHandler<DeleteSubjectCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(DeleteSubjectCommand command, CancellationToken ct)
    {
        await subjects.DeleteAsync(command.SubjectId, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}
