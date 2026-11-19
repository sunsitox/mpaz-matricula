using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace MarcelaPaz.Tests;

/// <summary>
/// Regresiones de la auditoría del 18-11-2026: el portal debe arrancar en
/// Production sin emisor SSO, la firma debe dejar la fecha que Dirección exige
/// para validar, y el expediente debe resolverse bajo la raíz configurada.
/// </summary>
public class CorreccionesAuditoriaTests
{
    [Fact]
    public void SsoSinConfigurar_EnProduccion_NoImpideElArranque()
    {
        var servicios = new ServiceCollection();

        servicios.AddEmisorSso(new ConfigurationBuilder().Build(), new EntornoPrueba("Production"));

        using var proveedor = servicios.BuildServiceProvider();

        Assert.False(proveedor.GetRequiredService<ConfiguracionSso>().Habilitado);
    }

    [Fact]
    public void SsoHabilitadoSinCertificado_EnProduccion_FallaConMensajeClaro()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sso:Habilitado"] = "true",
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddEmisorSso(configuracion, new EntornoPrueba("Production")));

        Assert.Contains("certificado", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegistrarFirma_GuardaLaFechaYHabilitaLaValidacion()
    {
        var (db, registro) = NuevoRegistro();
        var matricula = await AgregarMatriculaConFichaAsync(db, "FICHA_GENERADA");

        var error = await registro.RegistrarFirmaAsync(
            matricula.Id, DateOnly.FromDateTime(DateTime.Today));

        Assert.Null(error);
        Assert.Equal("FIRMADA", db.Matriculas.Single().Estado);
        Assert.NotNull(db.FichaVersiones.Single().FechaFirma);

        db.Documentos.Add(new Documento
        {
            EstudianteId = matricula.EstudianteId,
            MatriculaId = matricula.Id,
            Tipo = "FICHA_FIRMADA",
            NombreOriginal = "ficha.pdf",
            MimeType = "application/pdf",
            RutaAlmacenamiento = "1/ficha.pdf",
            HashSha256 = new string('a', 64),
            SubidoPor = 7,
            SubidoEn = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var validacion = await registro.ValidarDocumentacionAsync(
            matricula.Id, 9, "Caja 1", "VALIDADA", null);

        Assert.Null(validacion);
        Assert.Equal("VALIDADA", db.FichaVersiones.Single().EstadoValidacion);
    }

    [Fact]
    public async Task RegistrarFirma_SinFichaVigente_Avisa()
    {
        var (db, registro) = NuevoRegistro();
        var matricula = new MatriculaAnual { EstudianteId = 1, Anio = 2027, Tipo = "NUEVA", Estado = "FICHA_GENERADA" };
        db.Matriculas.Add(matricula);
        await db.SaveChangesAsync();

        var error = await registro.RegistrarFirmaAsync(
            matricula.Id, DateOnly.FromDateTime(DateTime.Today));

        Assert.NotNull(error);
        Assert.Equal("FICHA_GENERADA", db.Matriculas.Single().Estado);
    }

    [Fact]
    public void RutaDeDocumentos_UsaLaRaizConfigurada()
    {
        var raiz = ExpedienteDocumentos.ResolverRaiz("/srv/mpaz-data/matriculas/documentos", "/opt/mpaz/current");

        Assert.Equal(Path.GetFullPath("/srv/mpaz-data/matriculas/documentos"), raiz);
    }

    [Fact]
    public void RutaDeDocumentos_SinConfigurar_CaeEnAppData()
    {
        var raiz = ExpedienteDocumentos.ResolverRaiz(null, Path.GetTempPath());

        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "App_Data", "documentos")), raiz);
    }

    [Fact]
    public void ArchivoDelExpediente_SeEncuentraAunqueTengaElPrefijoAntiguo()
    {
        var raiz = Directory.CreateTempSubdirectory("mpaz-docs-").FullName;

        try
        {
            var carpeta = Path.Combine(raiz, "42");
            Directory.CreateDirectory(carpeta);

            var archivo = Path.Combine(carpeta, "ficha.pdf");
            File.WriteAllText(archivo, "%PDF-1.7");

            Assert.Equal(archivo, ExpedienteDocumentos.ResolverArchivo(raiz, "42/ficha.pdf"));
            Assert.Equal(archivo, ExpedienteDocumentos.ResolverArchivo(raiz, "App_Data/documentos/42/ficha.pdf"));
            Assert.Null(ExpedienteDocumentos.ResolverArchivo(raiz, "../fuera.pdf"));
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public async Task GuardarLaFichaSinCondicionMedica_CierraLaAnteriorSinBorrarla()
    {
        var (db, registro) = NuevoRegistro();

        var estudiante = new Estudiante
        {
            TipoIdentificador = "RUN",
            Identificador = "12345678-5",
            Nombres = "Prueba",
            ApellidoPaterno = "Salud",
            Nacionalidad = "Chilena",
            Domicilio = "Calle 1",
        };
        db.Estudiantes.Add(estudiante);
        await db.SaveChangesAsync();

        db.SituacionesMedicas.Add(new SituacionMedica
        {
            EstudianteId = estudiante.Id,
            Tipo = "CONDICION",
            Descripcion = "Asma: Controlada",
            EnTratamiento = true,
            VigenteDesde = new DateOnly(2026, 3, 1),
        });
        await db.SaveChangesAsync();

        var resultado = await registro.AplicarAsync(6, new Dictionary<string, string>
        {
            ["cédula de identidad"] = "12345678-5",
            ["situación médica (SI/NO)"] = "No",
        }, 2027);

        Assert.True(resultado.Guardado, resultado.Aviso);

        // MAT-14: el historial no se borra. La condición que dejó de declararse
        // queda cerrada con la fecha de hoy y sigue consultable.
        var situacion = Assert.Single(db.SituacionesMedicas);
        Assert.Equal("Asma: Controlada", situacion.Descripcion);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), situacion.VigenteHasta);
    }

    [Fact]
    public async Task RevisarVencidas_PersisteElEstadoSinDependerDeLaFicha()
    {
        var (db, registro) = NuevoRegistro();
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        db.Autorizaciones.AddRange(
            new Autorizacion
            {
                EstudianteId = 1,
                Tipo = "IMAGEN",
                Estado = "OTORGADA",
                FirmantePersonaId = 1,
                FechaFirma = hoy.AddYears(-1),
                VigenteDesde = hoy.AddYears(-1),
                VigenteHasta = hoy.AddDays(-1),
            },
            new Autorizacion
            {
                EstudianteId = 2,
                Tipo = "IMAGEN",
                Estado = "OTORGADA",
                FirmantePersonaId = 1,
                FechaFirma = hoy,
                VigenteDesde = hoy,
                VigenteHasta = hoy.AddDays(30),
            });
        await db.SaveChangesAsync();

        var marcadas = await registro.MarcarVencidasAsync(hoy);

        Assert.Equal(1, marcadas);
        Assert.Equal("VENCIDA", db.Autorizaciones.Single(a => a.EstudianteId == 1).Estado);
        Assert.Equal("OTORGADA", db.Autorizaciones.Single(a => a.EstudianteId == 2).Estado);
    }

    private static (MatriculaDbContext db, RegistroFicha registro) NuevoRegistro()
    {
        var opciones = new DbContextOptionsBuilder<MatriculaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MatriculaDbContext(opciones);

        return (db, new RegistroFicha(db));
    }

    private static async Task<MatriculaAnual> AgregarMatriculaConFichaAsync(
        MatriculaDbContext db,
        string estado)
    {
        var matricula = new MatriculaAnual
        {
            EstudianteId = 1,
            Anio = 2027,
            Tipo = "NUEVA",
            Estado = estado,
        };

        db.Matriculas.Add(matricula);
        await db.SaveChangesAsync();

        db.FichaVersiones.Add(new FichaVersion
        {
            MatriculaId = matricula.Id,
            Folio = "MP-2027-00001",
            Estado = "VIGENTE",
            NumeroVersion = 1,
            DatosSnapshot = "{}",
            GeneradaPor = 7,
            GeneradaEn = DateTimeOffset.Now.AddDays(-1),
        });
        await db.SaveChangesAsync();

        return matricula;
    }

    private sealed class EntornoPrueba(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;

        public string ApplicationName { get; set; } = "MarcelaPaz.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
