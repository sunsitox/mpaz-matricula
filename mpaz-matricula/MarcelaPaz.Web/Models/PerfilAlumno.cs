namespace MarcelaPaz.Web.Models;

/// <summary>
/// Ficha de consulta de un alumno: datos del registro único, historial de
/// matrículas, apoderados, contactos, retiros autorizados y salud declarada.
/// Sólo se muestran los vínculos y registros vigentes.
/// </summary>
public class PerfilAlumno
{
    /// <summary>Año escolar consultado: el activo o el elegido en el selector.</summary>
    public int Anio { get; set; }

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

    /// <summary>Vínculos familiares ya cerrados, con la fecha en que terminaron (MAT-14).</summary>
    public List<ApoderadoPerfil> ApoderadosAnteriores { get; set; } = new();

    public List<ContactoPerfil> Contactos { get; set; } = new();

    public List<RetiroPerfil> Retiros { get; set; } = new();

    public List<SituacionPerfil> Situaciones { get; set; } = new();

    /// <summary>Restricciones vigentes que afectan a algún vínculo familiar.</summary>
    public List<string> Restricciones { get; set; } = new();

    public string? CursoActual { get; set; }

    public string? EstadoActual { get; set; }

    /// <summary>Folio y versión de la ficha vigente del año (MAT-08).</summary>
    public string? FolioFicha { get; set; }

    public short? VersionFicha { get; set; }

    public int? MatriculaId { get; set; }

    /// <summary>Última modificación del expediente y funcionario responsable.
    /// Viene del trigger de auditoría; sin historial muestra updated_at y
    /// quien creó la matrícula.</summary>
    public DateTimeOffset? ActualizadaEn { get; set; }

    public string? ActualizadaPor { get; set; }

    /// <summary>La matrícula tiene ficha generada y puede confirmarse (MAT-04).</summary>
    public bool PuedeConfirmar { get; set; }

    /// <summary>La matrícula está en un estado anulable (BORRADOR, FICHA_GENERADA o FIRMADA).</summary>
    public bool PuedeAnular { get; set; }

    /// <summary>La sesión puede validar institucionalmente (Dirección) y la
    /// matrícula está formalizada sin validar.</summary>
    public bool PuedeValidar { get; set; }

    /// <summary>Fecha y responsable de la validación institucional (MAT-12).</summary>
    public DateTimeOffset? ValidadaEn { get; set; }

    public string? ValidadaPor { get; set; }

    /// <summary>Autorizaciones del apoderado con su estado e historial (MAT-04).</summary>
    public List<AutorizacionPerfil> Autorizaciones { get; set; } = new();

    /// <summary>Versiones de la ficha de la matrícula consultada (MAT-14).</summary>
    public List<VersionPerfil> Versiones { get; set; } = new();

    /// <summary>PIE y apoyos declarados (MAT-03).</summary>
    public string? PieParticipa { get; set; }

    public bool? PieDiagnostico { get; set; }

    public string? PieApoyos { get; set; }

    public string? Alergias { get; set; }

    public string? ContactoSanitarioNombre { get; set; }

    public string? ContactoSanitarioTelefono { get; set; }

    /// <summary>Requisitos documentales de la matrícula del año (MAT-06/07/09).</summary>
    public List<DocumentoPerfil> Documentos { get; set; } = new();

    // Qué bloques puede ver esta sesión. El servidor no consulta ni proyecta los
    // datos de un área sin su permiso; la vista usa estas banderas para explicar
    // la ausencia en lugar de mostrar una lista vacía (S-02).
    public bool PuedeVerFamilia { get; set; }

    public bool PuedeVerRetiro { get; set; }

    public bool PuedeVerSalud { get; set; }

    public bool PuedeVerPie { get; set; }

    public bool PuedeVerAutorizaciones { get; set; }

    public bool PuedeEditarAutorizaciones { get; set; }

    /// <summary>Hay una ficha vigente y la sesión puede generar versiones (MAT-18).</summary>
    public bool PuedeCorregirFicha { get; set; }

    /// <summary>El año consultado no tiene matrícula y la sesión puede crearla (MAT-01).</summary>
    public bool PuedeRenovarAnio { get; set; }

    public bool PuedeVerDocumentos { get; set; }

    public bool PuedeSubirDocumentos { get; set; }

    /// <summary>Escaneo vigente de la ficha firmada (MAT-09), si existe.</summary>
    public int? FichaFirmadaDocumentoId { get; set; }

    public string? FichaFirmadaNombre { get; set; }

    public DateTimeOffset? FichaFirmadaSubidaEn { get; set; }

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

        /// <summary>Fin del vínculo, cuando ya no está vigente.</summary>
        public DateOnly? Hasta { get; set; }
    }

    public class AutorizacionPerfil
    {
        public int Id { get; set; }

        public string Tipo { get; set; } = string.Empty;

        public string Etiqueta { get; set; } = string.Empty;

        /// <summary>PENDIENTE, OTORGADA, RECHAZADA, REVOCADA, VENCIDA o REEMPLAZADA.</summary>
        public string Estado { get; set; } = "PENDIENTE";

        public string EstadoEtiqueta { get; set; } = "Pendiente";

        public DateOnly? FechaFirma { get; set; }

        public DateOnly? VigenteDesde { get; set; }

        public DateOnly? VigenteHasta { get; set; }

        public DateTimeOffset? RevocadaEn { get; set; }

        public string? MotivoRevocacion { get; set; }

        public List<string> Alcances { get; set; } = new();
    }

    public class VersionPerfil
    {
        public int Id { get; set; }

        public short Numero { get; set; }

        public string Folio { get; set; } = string.Empty;

        public string Estado { get; set; } = string.Empty;

        public DateTimeOffset GeneradaEn { get; set; }

        public string? GeneradaPor { get; set; }

        public string? MotivoCorreccion { get; set; }
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

    public class DocumentoPerfil
    {
        public int RequisitoId { get; set; }

        public string Tipo { get; set; } = string.Empty;

        public string Etiqueta { get; set; } = string.Empty;

        public string Estado { get; set; } = "PENDIENTE";

        public int? DocumentoId { get; set; }

        public string? NombreArchivo { get; set; }

        public DateTimeOffset? SubidoEn { get; set; }

        /// <summary>GENERAL, SALUD, PIE o JUDICIAL.</summary>
        public string Sensibilidad { get; set; } = "GENERAL";

        public string? Observacion { get; set; }
    }
}
