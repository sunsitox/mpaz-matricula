using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MarcelaPaz.Tests;

/// <summary>
/// Comprueba la configuración de EF que sostiene el bloqueo optimista y el
/// mapeo de MAT-10, sin tocar la base: el modelo se construye en memoria y no
/// se abre ninguna conexión.
/// </summary>
public class ModeloPersistenciaTests
{
    private static MatriculaDbContext Crear() => new(
        new DbContextOptionsBuilder<MatriculaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=mpaz;Username=prueba;Password=prueba")
            .Options);

    [Fact]
    public void VersionDeLaMatricula_EsTestigoDeConcurrencia()
    {
        using var db = Crear();

        var version = db.Model
            .FindEntityType(typeof(MatriculaAnual))!
            .FindProperty(nameof(MatriculaAnual.Version))!;

        // El trigger de la base incrementa el valor; EF lo usa como testigo y
        // lo relee con RETURNING tras cada UPDATE.
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
    }

    [Fact]
    public void FichaVersion_MapeaLaValidacionDocumental()
    {
        using var db = Crear();

        var ficha = db.Model.FindEntityType(typeof(FichaVersion))!;

        Assert.Equal("ubicacion_original_fisico",
            ficha.FindProperty(nameof(FichaVersion.UbicacionOriginalFisico))!.GetColumnName());
        Assert.Equal("estado_validacion",
            ficha.FindProperty(nameof(FichaVersion.EstadoValidacion))!.GetColumnName());
        Assert.Equal("validado_por", ficha.FindProperty(nameof(FichaVersion.ValidadoPor))!.GetColumnName());
        Assert.Equal("validado_en", ficha.FindProperty(nameof(FichaVersion.ValidadoEn))!.GetColumnName());
        Assert.Equal("observacion_validacion",
            ficha.FindProperty(nameof(FichaVersion.ObservacionValidacion))!.GetColumnName());
        Assert.Equal("escaneo_documento_id",
            ficha.FindProperty(nameof(FichaVersion.EscaneoDocumentoId))!.GetColumnName());
    }

    [Fact]
    public void FichaVersion_EstadoValidacionPorDefectoEsPendiente()
    {
        Assert.Equal("PENDIENTE", new FichaVersion().EstadoValidacion);
    }
}
