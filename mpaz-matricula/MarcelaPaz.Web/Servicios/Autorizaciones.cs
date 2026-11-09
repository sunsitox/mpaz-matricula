namespace MarcelaPaz.Web.Servicios;

/// <summary>
/// Reglas de estado de las autorizaciones del apoderado (MAT-04). El estado se
/// guarda en la base (PENDIENTE, OTORGADA, RECHAZADA, REVOCADA, VENCIDA); el
/// vencimiento se calcula con la fecha de vigencia y también se persiste cuando
/// una autorización otorgada pasa su plazo.
/// </summary>
public static class Autorizaciones
{
    /// <summary>Una autorización otorgada cuya vigencia ya terminó está vencida.</summary>
    public static bool EstaVencida(string estado, DateOnly? vigenteHasta, DateOnly hoy) =>
        estado == "OTORGADA" && vigenteHasta is DateOnly hasta && hasta < hoy;

    /// <summary>Estado que se muestra: el guardado, salvo que ya haya vencido.</summary>
    public static string EstadoEfectivo(string estado, DateOnly? vigenteHasta, DateOnly hoy) =>
        EstaVencida(estado, vigenteHasta, hoy) ? "VENCIDA" : estado;

    public static string EtiquetaEstado(string estado) => estado switch
    {
        "OTORGADA" => "Otorgada",
        "RECHAZADA" => "Rechazada",
        "REVOCADA" => "Revocada",
        "VENCIDA" => "Vencida",
        "REEMPLAZADA" => "Reemplazada",
        _ => "Pendiente",
    };

    /// <summary>Sólo una autorización otorgada y vigente puede revocarse.</summary>
    public static bool PuedeRevocarse(string estadoEfectivo) => estadoEfectivo == "OTORGADA";
}
