namespace MarcelaPaz.Web.Models;

/// <summary>
/// Ficha de consulta de un alumno: datos del registro único, historial de
/// matrículas, apoderados, contactos, retiros autorizados y salud declarada.
/// Sólo se muestran los vínculos y registros vigentes.
/// </summary>
public class PerfilAlumno
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>RUN con puntos, como se muestra en pantalla.</summary>
    public string Rut { get; set; } = string.Empty;

    public DateOnly? FechaNacimiento { get; set; }

    public int? Edad { get; set; }

    public string? Nacionalidad { get; set; }

    public string? Domicilio { get; set; }

    public string? Comuna { get; set; }

    public bool? SaleSolo { get; set; }

    public bool? TieneSituacionMedica { get; set; }

    public string? NecesidadesAtencion { get; set; }

    public List<MatriculaPerfil> Matriculas { get; set; } = new();

    public List<ApoderadoPerfil> Apoderados { get; set; } = new();

    public List<ContactoPerfil> Contactos { get; set; } = new();

    public List<RetiroPerfil> Retiros { get; set; } = new();

    public List<SituacionPerfil> Situaciones { get; set; } = new();

    /// <summary>Restricciones vigentes que afectan a algún vínculo familiar.</summary>
    public List<string> Restricciones { get; set; } = new();

    public string? CursoActual { get; set; }

    public string? EstadoActual { get; set; }

    public class MatriculaPerfil
    {
        public short Anio { get; set; }

        public string? Curso { get; set; }

        public string Estado { get; set; } = string.Empty;

        public string Tipo { get; set; } = string.Empty;
    }

    public class ApoderadoPerfil
    {
        public string Nombre { get; set; } = string.Empty;

        public string Rut { get; set; } = string.Empty;

        public string Vinculo { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        public string? Correo { get; set; }

        public bool TieneRestriccion { get; set; }
    }

    public class ContactoPerfil
    {
        public string Nombre { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;

        public string Vinculo { get; set; } = string.Empty;
    }

    public class RetiroPerfil
    {
        public string Nombre { get; set; } = string.Empty;

        public string Rut { get; set; } = string.Empty;

        public string Vinculo { get; set; } = string.Empty;
    }

    public class SituacionPerfil
    {
        public string Tipo { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        public bool? EnTratamiento { get; set; }
    }
}
