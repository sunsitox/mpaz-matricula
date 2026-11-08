using System.Data;
using System.Data.Common;
using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MarcelaPaz.Web.Servicios;

/// <summary>Un año escolar registrado en el portal.</summary>
public sealed record AnioEscolarOpcion(int Anio, bool Activo, DateOnly? Apertura, DateOnly? Cierre);

/// <summary>
/// Año escolar con el que trabaja el módulo de Matrícula. Se lee de la tabla
/// <c>anio_escolar</c> (la define <c>database/05-anio-escolar.sql</c>) y nunca se
/// deduce de la fecha del sistema: el periodo de matrícula no coincide con el
/// calendario y en enero y febrero se sigue matriculando el año en curso.
/// Cambiar el año activo desde Configuración basta para que todo lo nuevo salga
/// con el año nuevo. Las fichas ya generadas conservan el suyo.
/// </summary>
public sealed class AnioEscolar
{
    private const string ClaveActivo = "anio-escolar:activo";

    private static readonly TimeSpan VigenciaCache = TimeSpan.FromMinutes(1);

    private readonly MatriculaDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AnioEscolar> _log;

    public AnioEscolar(MatriculaDbContext db, IMemoryCache cache, ILogger<AnioEscolar> log)
    {
        _db = db;
        _cache = cache;
        _log = log;
    }

    /// <summary>Año con matrícula activa, o <c>null</c> si todavía no se configuró.</summary>
    public async Task<int?> ActivoAsync()
    {
        if (_cache.TryGetValue(ClaveActivo, out int cacheado))
        {
            return cacheado;
        }

        try
        {
            await AplicarAperturaPorFechaAsync();

            // Año vigente: el marcado a mano o el más reciente cuya fecha de
            // apertura ya llegó. La automatización es sólo por apertura del
            // periodo, nunca por año calendario.
            var anio = await _db.Database
                .SqlQueryRaw<int>(
                    "select coalesce(max(anio), 0) as \"Value\" from anio_escolar "
                    + "where activo_matricula or (apertura is not null and apertura <= current_date)")
                .SingleAsync();

            if (anio == 0)
            {
                return null;
            }

            _cache.Set(ClaveActivo, anio, VigenciaCache);

            return anio;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            // Sin tabla o sin base el portal igual abre: quien la necesite avisa.
            _log.LogWarning(ex, "No se pudo leer el año escolar activo.");

            return null;
        }
    }

    /// <summary>Años registrados, del más reciente al más antiguo.</summary>
    public async Task<IReadOnlyList<AnioEscolarOpcion>> ListarAsync()
    {
        var anios = new List<AnioEscolarOpcion>();

        try
        {
            var conexion = _db.Database.GetDbConnection();
            var abrir = conexion.State != ConnectionState.Open;

            if (abrir)
            {
                await _db.Database.OpenConnectionAsync();
            }

            try
            {
                await using var comando = conexion.CreateCommand();
                comando.CommandText =
                    "select anio, activo_matricula, apertura, cierre from anio_escolar order by anio desc";

                await using var lector = await comando.ExecuteReaderAsync();

                while (await lector.ReadAsync())
                {
                    anios.Add(new AnioEscolarOpcion(
                        lector.GetInt16(0),
                        lector.GetBoolean(1),
                        lector.IsDBNull(2) ? null : DateOnly.FromDateTime(lector.GetDateTime(2)),
                        lector.IsDBNull(3) ? null : DateOnly.FromDateTime(lector.GetDateTime(3))));
                }
            }
            finally
            {
                if (abrir)
                {
                    await _db.Database.CloseConnectionAsync();
                }
            }
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            _log.LogWarning(ex, "No se pudo leer la lista de años escolares.");
        }

        // La pantalla debe mostrar como activo el mismo año que usa Matrícula,
        // incluido el que se abrió solo por fecha de apertura.
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var vigente = anios
            .Where(opcion => opcion.Activo
                             || (opcion.Apertura is DateOnly apertura && apertura <= hoy))
            .Select(opcion => opcion.Anio)
            .DefaultIfEmpty(0)
            .Max();

        return anios
            .Select(opcion => opcion with { Activo = opcion.Anio == vigente })
            .ToList();
    }

    /// <summary>
    /// Año para una consulta: el pedido si existe en el registro, si no el activo.
    /// Así el selector del panel puede abrir años anteriores sin que el módulo de
    /// Matrícula deje de usar siempre el año activo.
    /// </summary>
    public async Task<int?> ResolverAsync(int? pedido)
    {
        if (pedido is null or < 2000 or > 2100)
        {
            return await ActivoAsync();
        }

        var anios = await ListarAsync();

        return anios.Any(opcion => opcion.Anio == pedido.Value)
            ? pedido
            : await ActivoAsync();
    }

    /// <summary>
    /// Deja un año marcado como único activo. Devuelve false si no existe. Un año
    /// posterior con apertura ya cumplida se sigue usando: para volver a uno
    /// anterior hay que corregir esa fecha en Configuración.
    /// </summary>
    public async Task<bool> ActivarAsync(int anio)
    {
        try
        {
            await using var transaccion = await _db.Database.BeginTransactionAsync();

            await _db.Database.ExecuteSqlRawAsync(
                "update anio_escolar set activo_matricula = false, actualizado_en = now() "
                + "where activo_matricula and anio <> {0}",
                anio);

            var filas = await _db.Database.ExecuteSqlRawAsync(
                "update anio_escolar set activo_matricula = true, actualizado_en = now() where anio = {0}",
                anio);

            if (filas == 0)
            {
                await transaccion.RollbackAsync();

                return false;
            }

            await transaccion.CommitAsync();
        }
        catch (DbException ex)
        {
            _log.LogError(ex, "No se pudo activar el año escolar {Anio}.", anio);

            return false;
        }

        Invalidar();

        return true;
    }

    /// <summary>Registra un año nuevo. Devuelve el error, o null si quedó guardado.</summary>
    public async Task<string?> AgregarAsync(int anio, DateOnly? apertura, DateOnly? cierre)
    {
        if (anio is < 2000 or > 2100)
        {
            return "El año escolar debe estar entre 2000 y 2100.";
        }

        if (apertura is not null && cierre is not null && cierre < apertura)
        {
            return "El cierre del periodo no puede ser anterior a la apertura.";
        }

        // Las fechas son opcionales: se arma el INSERT con las columnas que
        // existen para no enviar parámetros nulos sin tipo a PostgreSQL.
        string sql;
        object[] parametros;

        if (apertura is null && cierre is null)
        {
            sql = "insert into anio_escolar (anio, activo_matricula) values ({0}, false)";
            parametros = new object[] { anio };
        }
        else if (cierre is null)
        {
            sql = "insert into anio_escolar (anio, activo_matricula, apertura) values ({0}, false, {1})";
            parametros = new object[] { anio, apertura!.Value };
        }
        else if (apertura is null)
        {
            sql = "insert into anio_escolar (anio, activo_matricula, cierre) values ({0}, false, {1})";
            parametros = new object[] { anio, cierre!.Value };
        }
        else
        {
            sql = "insert into anio_escolar (anio, activo_matricula, apertura, cierre) values ({0}, false, {1}, {2})";
            parametros = new object[] { anio, apertura.Value, cierre.Value };
        }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(sql, parametros);
        }
        catch (DbException ex) when (ex.SqlState == "23505")
        {
            return $"El año {anio} ya está registrado.";
        }
        catch (DbException ex)
        {
            _log.LogError(ex, "No se pudo registrar el año escolar {Anio}.", anio);

            return "No se pudo registrar el año: revise la conexión con la base de datos.";
        }

        Invalidar();

        return null;
    }

    /// <summary>
    /// Apertura automática por fecha, nunca por año calendario: si el periodo de
    /// un año ya abrió, lo activa cuando no hay año activo o cuando el periodo
    /// del activo ya cerró. No pisa una activación manual cuyo periodo sigue
    /// abierto o que no declara fechas.
    /// </summary>
    public async Task AplicarAperturaPorFechaAsync()
    {
        const string sql = """
            do $$
            declare
                candidato smallint;
                activo smallint;
                cerrado boolean;
            begin
                select a.anio into candidato
                from anio_escolar a
                where a.apertura is not null
                  and a.apertura <= current_date
                  and (a.cierre is null or current_date <= a.cierre)
                order by a.anio desc
                limit 1;

                if candidato is null then
                    return;
                end if;

                select a.anio,
                       (a.cierre is not null and a.cierre < current_date)
                  into activo, cerrado
                from anio_escolar a
                where a.activo_matricula
                limit 1;

                if activo is not null and activo <> candidato
                   and not coalesce(cerrado, false) then
                    return;
                end if;

                update anio_escolar
                   set activo_matricula = false,
                       actualizado_en = now()
                 where activo_matricula
                   and anio <> candidato;

                update anio_escolar
                   set activo_matricula = true,
                       actualizado_en = now()
                 where anio = candidato
                   and not activo_matricula;
            end $$;
            """;

        try
        {
            await _db.Database.ExecuteSqlRawAsync(sql);
        }
        catch (DbException ex)
        {
            _log.LogWarning(ex, "No se pudo evaluar la apertura automática del periodo de matrícula.");
        }
    }

    /// <summary>Descarta el año activo en caché (tras activar uno nuevo).</summary>
    public void Invalidar() => _cache.Remove(ClaveActivo);
}
