namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Ficha o certificado médico de un funcionario (tabla
/// <c>ficha_medica_funcionario</c>). Es información sensible: sólo UTP la
/// consulta y registra. El archivo adjunto es opcional y se guarda fuera de
/// <c>wwwroot</c>, igual que los documentos del expediente.
/// </summary>
public class FichaMedicaFuncionario
{
    public int Id { get; set; }

    public int FuncionarioId { get; set; }

    /// <summary>Tipo de documento: certificado, licencia, ficha médica, etc.</summary>
    public string Tipo { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public string? Emisor { get; set; }

    public DateOnly? EmitidoEn { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    /// <summary>Ruta relativa del archivo dentro de la carpeta de documentos.</summary>
    public string? ArchivoRuta { get; set; }

    public string? ArchivoNombre { get; set; }

    public int? RegistradoPor { get; set; }

    public DateTimeOffset RegistradoEn { get; set; } = DateTimeOffset.UtcNow;
}
