using MarcelaPaz.Web.Models;

namespace MarcelaPaz.Tests;

// Aceptación del CSV de MAT-16: formato que abre Excel y sin fórmulas.
public class ReportesCsvTests
{
    [Fact]
    public void Campo_EscapaSeparadorYComillas()
    {
        Assert.Equal("\"a;b\"", CsvReporte.Campo("a;b"));
        Assert.Equal("\"di \"\"x\"\" jo\"", CsvReporte.Campo("di \"x\" jo"));
        Assert.Equal("normal", CsvReporte.Campo("normal"));
        Assert.Equal(string.Empty, CsvReporte.Campo(null));
    }

    [Fact]
    public void Campo_NeutralizaFormulas()
    {
        Assert.Equal("'=1+1", CsvReporte.Campo("=1+1"));
        Assert.Equal("'+hola", CsvReporte.Campo("+hola"));
        Assert.Equal("'-x", CsvReporte.Campo("-x"));
        Assert.Equal("'@x", CsvReporte.Campo("@x"));
    }

    [Fact]
    public void Matriculas_EncabezadoYFilas()
    {
        var filas = new[]
        {
            new ReporteMatriculas.FilaMatricula
            {
                Estudiante = "Ana Soto",
                Rut = "12.345.678-9",
                Curso = "1° Básico A",
                Estado = "FIRMADA",
                Tipo = "NUEVA",
                Folio = "MP-2027-00001",
                DocsPendientes = 0,
            },
            new ReporteMatriculas.FilaMatricula
            {
                Estudiante = "Luis; Pardo",
                Rut = "9.876.543-2",
                Estado = "Sin matrícula",
                Tipo = "—",
            },
        };

        var csv = CsvReporte.Matriculas(filas);
        var lineas = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lineas.Length);
        Assert.Equal("Estudiante;RUT;Curso;Estado;Tipo;Folio;DocsPendientes", lineas[0]);
        Assert.Contains("MP-2027-00001", lineas[1]);
        Assert.Contains("\"Luis; Pardo\"", lineas[2]);
    }

    [Fact]
    public void Faltantes_EncabezadoYFilas()
    {
        var filas = new[]
        {
            new ReporteFaltantes.FilaFaltante
            {
                Estudiante = "Ana Soto",
                Rut = "12.345.678-9",
                Curso = "1° Básico A",
                Etiqueta = "Cédula de identidad",
                RegistradoEn = new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(-3)),
            },
        };

        var csv = CsvReporte.Faltantes(filas);
        var lineas = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lineas.Length);
        Assert.Equal("Estudiante;RUT;Curso;Documento;RegistradoEn", lineas[0]);
        Assert.Contains("09-11-2026", lineas[1]);
    }
}
