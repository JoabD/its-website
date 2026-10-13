using ClosedXML.Excel;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Spreadsheets;

/// <summary>
/// Genera la plantilla .xlsx de "Agregar alumno" → Excel (columnas exactas que espera
/// ImportStudentsCommandHandler — ver el comentario de esa clase). Comparte ClosedXML con
/// SpreadsheetReader, pero es una implementación separada (ISpreadsheetWriter) porque leer y
/// escribir son responsabilidades distintas (spec técnico §4-O).
///
/// Ajuste 2026-09 (feedback real: "Fila 3: No existe una región activa llamada 'Region Cuautla'" —
/// error de dedo al escribir el nombre a mano): REGION, MODALIDAD y PLAN ya no son texto libre en la
/// plantilla — son dropdowns reales de Excel (validación de datos tipo lista), alimentados por una
/// hoja auxiliar oculta ("Listas") con los valores válidos, para que quien llena la plantilla
/// seleccione en vez de escribir y así sea imposible teclear un nombre de región o una modalidad que
/// no existan.
/// </summary>
public sealed class SpreadsheetWriter : ISpreadsheetWriter
{
    private static readonly string[] Headers =
    [
        "NOMBRE_COMPLETO", "CORREO", "TELEFONO", "FECHA_NACIMIENTO", "REGION", "MODALIDAD", "PLAN", "CUATRIMESTRE_O_SEMESTRE",
    ];

    private static readonly string[] ModalityLabels = ["Presencial", "Virtual", "Diplomado"];
    private static readonly string[] PlanLabels = ["Cuatrimestral", "Semestral"];

    private const int TemplateRowCount = 500;

    public byte[] BuildStudentImportTemplate(IReadOnlyList<StudentImportTemplateRegion> regions)
    {
        using var workbook = new XLWorkbook();

        // Importante: "Alumnos" debe ser la PRIMERA hoja del libro (índice 0). El importador
        // (SpreadsheetReader, compartido con ImportPayments) lee siempre `Worksheets.First()` — es
        // genérico y asume que la primera hoja es la de datos. Si "Listas" se agregaba antes que
        // "Alumnos" (como en la primera versión de este cambio), el importador terminaba leyendo la
        // hoja auxiliar de listas en vez de los alumnos capturados (bug real detectado 2026-09:
        // "Fila: Nombre es requerido" + conteo de filas absurdo tipo "0 de 5" con solo 2 alumnos
        // capturados — esas filas eran en realidad las de la hoja "Listas"). Por eso "Alumnos" se
        // crea (reserva su posición) primero, aunque se llene después de tener listSheet listo.
        var dataSheet = workbook.Worksheets.Add("Alumnos");
        var listSheet = BuildListsSheet(workbook, regions);
        BuildDataSheet(dataSheet, regions, listSheet);
        BuildInstructionsSheet(workbook, regions);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Hoja auxiliar oculta ("muy oculta" — no aparece ni con clic derecho → Mostrar, solo se ve
    /// editando el proyecto en el editor de VBA, así un usuario normal no la borra ni la edita por
    /// accidente) que alimenta los tres dropdowns de la hoja "Alumnos". Nunca se lee en la
    /// importación — solo existe para que Excel tenga de dónde sacar las listas. Se agrega DESPUÉS
    /// de "Alumnos" para no quitarle el índice 0 (ver comentario en BuildStudentImportTemplate).
    /// </summary>
    private static IXLWorksheet BuildListsSheet(XLWorkbook workbook, IReadOnlyList<StudentImportTemplateRegion> regions)
    {
        var sheet = workbook.Worksheets.Add("Listas");

        sheet.Cell(1, 1).Value = "Región";
        var regionNames = regions.Count > 0 ? regions.Select(r => r.Name).ToList() : [""];
        for (var i = 0; i < regionNames.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = regionNames[i];
        }

        sheet.Cell(1, 2).Value = "Modalidad";
        for (var i = 0; i < ModalityLabels.Length; i++)
        {
            sheet.Cell(i + 2, 2).Value = ModalityLabels[i];
        }

        sheet.Cell(1, 3).Value = "Plan";
        for (var i = 0; i < PlanLabels.Length; i++)
        {
            sheet.Cell(i + 2, 3).Value = PlanLabels[i];
        }

        sheet.Visibility = XLWorksheetVisibility.VeryHidden;
        return sheet;
    }

    private static void BuildDataSheet(IXLWorksheet sheet, IReadOnlyList<StudentImportTemplateRegion> regions, IXLWorksheet listSheet)
    {
        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 59, 87);
        }

        // Fila de ejemplo (2): usa la primera región activa real para que el valor de REGION sea
        // válido de inmediato si el admin la deja tal cual — debe borrarla o sobrescribirla con sus
        // propios datos antes de importar, por eso queda en gris/cursiva.
        var exampleRegion = regions.Count > 0 ? regions[0].Name : "Región San Miguel";
        var exampleModality = regions.Count > 0 && !regions[0].ModalityScope.Contains(Modality.Onsite)
            ? ModalityLabelFor(regions[0].ModalityScope[0])
            : "Presencial";
        string[] example = ["Juan Pérez López", "juan.perez@example.com", "5512345678", "15/03/1998", exampleRegion, exampleModality, "Cuatrimestral", "1"];
        for (var i = 0; i < example.Length; i++)
        {
            sheet.Cell(2, i + 1).Value = example[i];
        }

        sheet.Row(2).Style.Font.FontColor = XLColor.Gray;
        sheet.Row(2).Style.Font.Italic = true;

        // Dropdowns reales (validación de datos tipo lista) para REGION, MODALIDAD y PLAN — evita
        // errores de dedo al escribir el nombre de una región o de una modalidad a mano. Se aplican
        // a un bloque grande de filas (no solo a la fila de ejemplo) para que sigan funcionando
        // según se van agregando alumnos.
        var regionListRange = listSheet.Range(2, 1, Math.Max(2, regions.Count + 1), 1);
        var modalityListRange = listSheet.Range(2, 2, 1 + ModalityLabels.Length, 2);
        var planListRange = listSheet.Range(2, 3, 1 + PlanLabels.Length, 3);

        sheet.Range(2, 5, TemplateRowCount, 5).CreateDataValidation().List(regionListRange, true); // REGION
        sheet.Range(2, 6, TemplateRowCount, 6).CreateDataValidation().List(modalityListRange, true); // MODALIDAD
        sheet.Range(2, 7, TemplateRowCount, 7).CreateDataValidation().List(planListRange, true); // PLAN

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        sheet.Column(2).Width = Math.Max(sheet.Column(2).Width, 26); // CORREO
    }

    private static void BuildInstructionsSheet(XLWorkbook workbook, IReadOnlyList<StudentImportTemplateRegion> regions)
    {
        var sheet = workbook.Worksheets.Add("Instrucciones");
        var row = 1;

        void Title(string text)
        {
            var cell = sheet.Cell(row, 1);
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 13;
            row += 2;
        }

        void Line(string text)
        {
            sheet.Cell(row, 1).Value = text;
            row++;
        }

        Title("Cómo llenar la plantilla de alta de alumnos");
        Line("1. No cambies los nombres de las columnas en la hoja \"Alumnos\" ni el orden en que están.");
        Line("2. Borra o sobrescribe la fila de ejemplo (fila 2, en gris) con los datos reales.");
        Line("3. Agrega un renglón por cada alumno a partir de la fila 2.");
        Line("4. REGION, MODALIDAD y PLAN son listas desplegables: haz clic en la celda y elige el valor de la flechita — no los escribas a mano, así evitas errores como \"no existe una región activa llamada...\".");
        Line("5. La MODALIDAD elegida debe ser una que esa región realmente ofrezca (ver la lista de abajo) — si no, la fila se rechaza al importar.");
        Line("6. Guarda el archivo como .xlsx (o .csv, aunque el .csv pierde los dropdowns) y súbelo en \"Agregar alumno\" → pestaña Excel.");
        row++;

        Title("Formato esperado por columna");
        Line("NOMBRE_COMPLETO — texto libre, obligatorio.");
        Line("CORREO — opcional. Si lo capturas, debe ser válido y único (no debe existir ya en el sistema). Sin correo, el alumno queda registrado (matrícula, materias, calificaciones y pagos) pero sin acceso al sistema por ahora.");
        Line("TELEFONO — opcional, solo números. Sin teléfono, el alumno queda registrado pero sin la opción de \"enviar por WhatsApp\".");
        Line("FECHA_NACIMIENTO — formato dd/mm/aaaa (ej. 15/03/1998).");
        Line("REGION — elige de la lista desplegable una región activa (ver detalle abajo).");
        Line("MODALIDAD — elige de la lista desplegable: Presencial, Virtual o Diplomado (debe ser una que la región elegida ofrezca).");
        Line("PLAN — elige de la lista desplegable: Cuatrimestral o Semestral.");
        Line("CUATRIMESTRE_O_SEMESTRE — número del 1 al 6, el punto en el que entra el alumno.");
        row++;

        Title("Regiones activas y modalidades que ofrece cada una");
        if (regions.Count == 0)
        {
            Line("No hay regiones activas configuradas todavía — crea una en Configuración → Regiones antes de importar.");
        }
        else
        {
            foreach (var region in regions)
            {
                var modalities = region.ModalityScope.Count > 0
                    ? string.Join(", ", region.ModalityScope.Select(ModalityLabelFor))
                    : "sin modalidades configuradas";
                Line($"{region.Name} — {region.Abbreviation} (modalidades: {modalities})");
            }
        }

        sheet.Column(1).Width = 90;
        sheet.Column(1).Style.Alignment.WrapText = false;
    }

    private static string ModalityLabelFor(Modality modality) => modality switch
    {
        Modality.Onsite => "Presencial",
        Modality.Online => "Virtual",
        Modality.Diploma => "Diplomado",
        _ => modality.ToString(),
    };

    /// <summary>
    /// Plantilla de importación de calificaciones (Kardex → Calificaciones → Importar desde Excel):
    /// una plantilla por (periodo, plan, cuatrimestre/semestre, región) — cubre TODAS las materias
    /// de ese grupo en un solo archivo, tal como lo pidió el cliente ("columna A los alumnos y en
    /// columnas diferentes la materia"). MATRICULA y ALUMNO vienen prellenados con los alumnos
    /// realmente inscritos (no son editables en la práctica: el importador solo mira MATRICULA para
    /// ubicar al alumno, así que aunque se edite ALUMNO no tiene efecto — se deja de solo lectura
    /// visualmente para evitar confusión). Sin fila de ejemplo aparte: los renglones ya son datos
    /// reales del grupo, así que no hace falta inventar uno (a diferencia de la plantilla de alta de
    /// alumnos, que parte de una hoja vacía).
    /// </summary>
    public byte[] BuildGradeImportTemplate(
        string periodLabel, string planLabel, int termNumber, string regionName,
        IReadOnlyList<GradeImportTemplateSubject> subjects, IReadOnlyList<GradeImportTemplateStudent> students)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Calificaciones");

        var headers = new List<string> { "MATRICULA", "ALUMNO" };
        headers.AddRange(subjects.Select(s => s.SubjectName.ToUpperInvariant()));

        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(26, 39, 68);
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        sheet.Row(1).Height = 32;

        var readonlyFill = XLColor.FromArgb(238, 240, 244);
        var row = 2;
        foreach (var student in students)
        {
            sheet.Cell(row, 1).Value = student.EnrollmentNumber;
            sheet.Cell(row, 2).Value = student.FullName;
            sheet.Cell(row, 1).Style.Fill.BackgroundColor = readonlyFill;
            sheet.Cell(row, 2).Style.Fill.BackgroundColor = readonlyFill;
            row++;
        }

        var lastStudentRow = Math.Max(2, row - 1);

        // Validación de datos: entero 0-10 en cada columna de materia (el mínimo aprobatorio real se
        // aplica al importar, no aquí — esta validación solo evita capturar algo fuera de rango).
        for (var i = 0; i < subjects.Count; i++)
        {
            var col = 3 + i;
            var dv = sheet.Range(2, col, lastStudentRow, col).CreateDataValidation();
            dv.WholeNumber.Between(0, 10);
            dv.ErrorTitle = "Calificación inválida";
            dv.ErrorMessage = "La calificación debe ser un número entero entre 0 y 10.";
            dv.InputTitle = "Calificación";
            dv.InputMessage = "Entero de 0 a 10. Deja la celda vacía si el alumno aún no tiene calificación.";
        }

        sheet.Column(1).Width = 14;
        sheet.Column(2).Width = 34;
        for (var i = 0; i < subjects.Count; i++)
        {
            sheet.Column(3 + i).Width = 20;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.SheetView.FreezeColumns(2);

        BuildGradeInstructionsSheet(workbook, periodLabel, planLabel, termNumber, regionName, subjects);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void BuildGradeInstructionsSheet(
        XLWorkbook workbook, string periodLabel, string planLabel, int termNumber, string regionName,
        IReadOnlyList<GradeImportTemplateSubject> subjects)
    {
        var sheet = workbook.Worksheets.Add("Instrucciones");
        var row = 1;

        void Title(string text)
        {
            var cell = sheet.Cell(row, 1);
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 13;
            row += 2;
        }

        void Line(string text)
        {
            sheet.Cell(row, 1).Value = text;
            row++;
        }

        Title($"Plantilla de calificaciones — {periodLabel} · {planLabel} · {(planLabel == "Semestral" ? "Semestre" : "Cuatrimestre")} {termNumber} · {regionName}");
        Line("1. No modifiques la hoja \"Calificaciones\": ni el orden de las columnas, ni los encabezados, ni las filas ya prellenadas.");
        Line("2. Las columnas MATRICULA y ALUMNO vienen prellenadas con los alumnos ya inscritos en este grupo — no se editan; el sistema ubica a cada alumno por su MATRICULA.");
        Line("3. Cada columna de materia corresponde exactamente a una materia de este grupo. Captura ahí la calificación de cada alumno.");
        Line("4. La calificación debe ser un número entero de 0 a 10 (sin decimales). Excel avisará si escribes un valor fuera de rango.");
        Line("5. Deja la celda vacía si el alumno todavía no tiene calificación — no se importará como reprobado, quedará como \"Sin calificar\".");
        Line("6. Guarda el archivo y súbelo en: Calificaciones → Importar desde Excel.");
        row++;

        Title("Qué pasa si algo sale mal");
        Line("El sistema revisa el archivo fila por fila y columna por columna. Si una calificación está fuera de rango o una matrícula no pertenece a este grupo, esa celda se reporta con el motivo exacto — el resto del archivo se importa normalmente.");
        row++;

        Title("Materias de este grupo");
        foreach (var subject in subjects)
        {
            Line(subject.SubjectName);
        }

        sheet.Column(1).Width = 100;
    }
}
