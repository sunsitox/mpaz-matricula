namespace MarcelaPaz.Web.Models;

/// <summary>Estado de las matrículas del año (MAT-16).</summary>
public class ReporteMatriculas
{
    public int Anio { get; set; }

    public List<int> Anios { get; set; } = new();

    /// <summary>Conteo por estado, en orden de avance, más «Sin matrícula».</summary>
    public List<ResumenEstado> Resumen { get; set; } = new();

    public List<FilaMatricula> Filas { get; set; } = new();

    public class ResumenEstado
    {
        public string Estado { get; set; } = string.Empty;

        public int Total { get; set; }
    }

    public class FilaMatricula
    {
        public int EstudianteId { get; set; }

        public string Estudiante { get; set; } = string.Empty;

        public string Rut { get; set; } = string.Empty;

        public string? Curso { get; set; }

        public string Estado { get; set; } = string.Empty;

        public string Tipo { get; set; } = string.Empty;

        public string? Folio { get; set; }

        public int DocsPendientes { get; set; }
    }
}

/// <summary>Requisitos documentales pendientes del año (MAT-16).</summary>
public class ReporteFaltantes
{
    public int Anio { get; set; }

    public List<int> Anios { get; set; } = new();

    public List<FilaFaltante> Filas { get; set; } = new();

    public class FilaFaltante
    {
        public int EstudianteId { get; set; }

        public string Estudiante { get; set; } = string.Empty;

        public string Rut { get; set; } = string.Empty;

        public string? Curso { get; set; }

        public string TipoDocumento { get; set; } = string.Empty;

        public string Etiqueta { get; set; } = string.Empty;

        public DateTimeOffset RegistradoEn { get; set; }
    }
}

/// <summary>
/// CSV separado por punto y coma (el que abre Excel en español) con BOM.
/// Escapa el separador, las comillas y neutraliza fórmulas al inicio.
/// </summary>
public static class CsvReporte
{
    public static string Campo(string? valor)
    {
        valor ??= string.Empty;

        if (valor.Length > 0 && "=+-@".Contains(valor[0]))
        {
            valor = "'" + valor;
        }

        return valor.Contains(';') || valor.Contains('"') || valor.Contains('\n') || valor.Contains('\r')
            ? "\"" + valor.Replace("\"", "\"\"") + "\""
            : valor;
    }

    public static string Linea(params string?[] campos) =>
        string.Join(";", campos.Select(Campo));

    public static string Matriculas(IEnumerable<ReporteMatriculas.FilaMatricula> filas)
    {
        var lineas = new List<string>
        {
            Linea("Estudiante", "RUT", "Curso", "Estado", "Tipo", "Folio", "DocsPendientes"),
        };

        lineas.AddRange(filas.Select(f => Linea(
            f.Estudiante, f.Rut, f.Curso ?? string.Empty, f.Estado, f.Tipo,
            f.Folio ?? string.Empty, f.DocsPendientes.ToString())));

        return string.Join("\r\n", lineas) + "\r\n";
    }

    public static string Faltantes(IEnumerable<ReporteFaltantes.FilaFaltante> filas)
    {
        var lineas = new List<string>
        {
            Linea("Estudiante", "RUT", "Curso", "Documento", "RegistradoEn"),
        };

        lineas.AddRange(filas.Select(f => Linea(
            f.Estudiante, f.Rut, f.Curso ?? string.Empty, f.Etiqueta,
            f.RegistradoEn.ToLocalTime().ToString("dd-MM-yyyy"))));

        return string.Join("\r\n", lineas) + "\r\n";
    }
}
