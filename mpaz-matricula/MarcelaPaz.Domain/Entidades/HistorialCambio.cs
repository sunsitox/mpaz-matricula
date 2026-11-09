namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Fila de <c>historial_cambios</c>: la escribe el trigger de auditoría en
/// cada INSERT/UPDATE/DELETE, con el funcionario de la sesión. Solo lectura
/// desde la aplicación (sin clave: nunca se inserta ni modifica por EF).
/// </summary>
public class HistorialCambio
{
    public long Id { get; set; }

    public string Tabla { get; set; } = string.Empty;

    public long RegistroId { get; set; }

    public string Accion { get; set; } = string.Empty;

    public int? FuncionarioId { get; set; }

    public DateTimeOffset CambiadoEn { get; set; }
}
