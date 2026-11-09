using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Servicios;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Tests;

// Aceptación de la validación institucional (MAT-12): acto de Dirección
// distinto de confirmar, sobre matrícula formalizada.
public class ValidacionMatriculaTests
{
    [Theory]
    [InlineData("FIRMADA")]
    [InlineData("CONFIRMADA")]
    public async Task Validar_FirmadaOConfirmada_RegistraFechaYResponsable(string estado)
    {
        var (db, registro) = NuevoRegistro();
        var matricula = await AgregarMatriculaAsync(db, estado);

        var error = await registro.ValidarAsync(matricula.Id, 7);

        Assert.Null(error);
        var vigente = db.Matriculas.Single();
        Assert.NotNull(vigente.ValidadaEn);
        Assert.Equal(7, vigente.ValidadaPor);
    }

    [Theory]
    [InlineData("BORRADOR")]
    [InlineData("FICHA_GENERADA")]
    [InlineData("ANULADA")]
    public async Task Validar_SinFormalizar_Rechaza(string estado)
    {
        var (db, registro) = NuevoRegistro();
        var matricula = await AgregarMatriculaAsync(db, estado);

        var error = await registro.ValidarAsync(matricula.Id, 7);

        Assert.NotNull(error);
        Assert.Null(db.Matriculas.Single().ValidadaEn);
    }

    [Fact]
    public async Task Validar_YaValidada_EsIdempotente()
    {
        var (db, registro) = NuevoRegistro();
        var matricula = await AgregarMatriculaAsync(db, "FIRMADA");
        Assert.Null(await registro.ValidarAsync(matricula.Id, 7));
        var primera = db.Matriculas.Single().ValidadaEn;

        var error = await registro.ValidarAsync(matricula.Id, 9);

        Assert.Null(error);
        Assert.Equal(primera, db.Matriculas.Single().ValidadaEn);
        Assert.Equal(7, db.Matriculas.Single().ValidadaPor);
    }

    [Fact]
    public async Task Validar_Inexistente_Avisa()
    {
        var (_, registro) = NuevoRegistro();

        var error = await registro.ValidarAsync(999999, 7);

        Assert.NotNull(error);
    }

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
