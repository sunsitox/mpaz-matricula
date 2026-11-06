using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Infrastructure.Persistence;

/// <summary>
/// Ejecuta una escritura dentro de una transacción y publica el funcionario
/// responsable en la sesión de PostgreSQL, para que los triggers del DDL
/// (<c>historial_cambios</c>) registren quién hizo el cambio.
/// </summary>
public class EscrituraAuditada
{
    private readonly MatriculaDbContext _db;

    public EscrituraAuditada(MatriculaDbContext db) => _db = db;

    public async Task EjecutarAsync(
        int funcionarioId,
        Func<CancellationToken, Task> cambios,
        CancellationToken ct = default)
    {
        var estrategia = _db.Database.CreateExecutionStrategy();

        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await _db.Database.BeginTransactionAsync(ct);

            // set_config(..., true) equivale a SET LOCAL y admite parámetros.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.funcionario_id', {funcionarioId.ToString()}, true)", ct);

            await cambios(ct);
            await _db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });
    }
}
