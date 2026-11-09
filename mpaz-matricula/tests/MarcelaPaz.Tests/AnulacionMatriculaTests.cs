using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Servicios;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Tests;

// Aceptación del estado ANULADA: transiciones permitidas, motivo obligatorio
// y protección de la matrícula confirmada.
public class AnulacionMatriculaTests
{
    [Fact]
    public async Task Anular_Borrador_ConMotivo_DejaAnulada()
    {
        var (db, registro) = NuevoRegistro();
        var matricula = await AgregarMatriculaAsync(db, EstadoBorrador);

        var error = await registro.AnularAsync(matricula.Id, MotivoDuplicada);

        Assert.Null(error);
        Assert.Equal(EstadoAnulada, db.Matriculas.Single().Estado);
        Assert.Equal(MotivoDuplicada, db.Matriculas.Single().Observaciones);
    }

    [Theory]
    [InlineData(EstadoFichaGenerada)]
    [InlineData(EstadoFirmada)]
    public async Task Anular_FichaGeneradaOFirmada_Permite(string estado)
    {
        var (db, registro) = NuevoRegistro();
        var matricula = await AgregarMatriculaAsync(db, estado);

        var error = await registro.AnularAsync(matricula.Id, MotivoRetiro);

        Assert.Null(error);
        Assert.Equal(EstadoAnulada, db.Matriculas.Single().Estado);
    }

    [Fact]
    public async Task Anular_SinMotivo_RechazaYSinCambios()
    {
        var (db, registro) = NuevoRegistro();
        var matricula = await AgregarMatriculaAsync(db, EstadoBorrador);

        var error = await registro.AnularAsync(matricula.Id, EspaciosEnBlanco);

        Assert.NotNull(error);
        Assert.Equal(EstadoBorrador, db.Matriculas.Single().Estado);
    }

    [Fact]
    public async Task Anular_Confirmada_Rechaza()
    {
        var (db, registro) = NuevoRegistro();
        var matricula = await AgregarMatriculaAsync(db, EstadoConfirmada);

        var error = await registro.AnularAsync(matricula.Id, MotivoRetiro);

        Assert.NotNull(error);
        Assert.Equal(EstadoConfirmada, db.Matriculas.Single().Estado);
    }

    [Fact]
    public async Task Anular_YaAnulada_EsIdempotente()
    {
        var (db, registro) = NuevoRegistro();
        var matricula = await AgregarMatriculaAsync(db, EstadoAnulada);

        var error = await registro.AnularAsync(matricula.Id, MotivoRetiro);

        Assert.Null(error);
        Assert.Equal(EstadoAnulada, db.Matriculas.Single().Estado);
    }

    [Fact]
    public async Task Anular_Inexistente_Avisa()
    {
        var (_, registro) = NuevoRegistro();

        var error = await registro.AnularAsync(999999, MotivoRetiro);

        Assert.NotNull(error);
    }

    private const string EstadoBorrador = "BORRADOR";
    private const string EstadoFichaGenerada = "FICHA_GENERADA";
    private const string EstadoFirmada = "FIRMADA";
    private const string EstadoConfirmada = "CONFIRMADA";
    private const string EstadoAnulada = "ANULADA";
    private const string MotivoDuplicada = "Matrícula duplicada";
    private const string MotivoRetiro = "Retiro del estudiante";
    private const string EspaciosEnBlanco = "  ";

    private static (MatriculaDbContext db, RegistroFicha registro) NuevoRegistro()
    {
        var opciones = new DbContextOptionsBuilder<MatriculaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MatriculaDbContext(opciones);
        return (db, new RegistroFicha(db));
    }

    private static async Task<MatriculaAnual> AgregarMatriculaAsync(MatriculaDbContext db, string estado)
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
        return matricula;
    }
}
