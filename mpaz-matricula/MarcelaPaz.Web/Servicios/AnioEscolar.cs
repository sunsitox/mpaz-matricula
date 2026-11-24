using System.Data;
using System.Data.Common;
using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MarcelaPaz.Web.Servicios;

/// <summary>Un año escolar registrado en el portal.</summary>
public sealed record AnioEscolarOpcion(int Anio, bool Activo, DateOnly? Apertura, DateOnly? Cierre, int Matriculas);

/// <summary>
/// Año escolar con el que trabaja el módulo de Matrícula. Se lee de la tabla
/// <c>anio_escolar</c> (la define <c>database/pasos/05-anio-escolar.sql</c>) y nunca se
/// deduce de la fecha del sistema: el periodo de matrícula no coincide con el
/// calendario y en enero y febrero se sigue matriculando el año en curso.
/// Cambiar el año activo desde Configuración basta para que todo lo nuevo salga
/// con el año nuevo. Las fichas ya generadas conservan el suyo.
/// </summary>
public sealed class AnioEscolar
{
    private const string ClaveActivo = "anio-escolar:activo";
    private const string ClaveApertura = "anio-escolar:apertura";
    private const string ClaveLista = "anio-escolar:lista";

    private static readonly TimeSpan VigenciaCache = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Año vigente en una sola consulta: el activado a mano y, sólo si no hay
    /// ninguno, el más reciente cuya apertura ya llegó. Lo usan la lectura del
    /// año y la validación del borrado para que ambas reglas no se separen.
    /// </summary>
    private const string SqlVigente =
        "select coalesce("
        + "(select a.anio from anio_escolar a where a.activo_matricula "
        + "order by a.anio desc limit 1), "
        + "(select max(a.anio) from anio_escolar a "
        + "where a.apertura is not null and a.apertura <= current_date), "
        + "0) as \"Value\"";

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
            // La apertura automática escribe en la base (un bloque DO). Se evalúa
            // una vez por minuto y no en cada consulta del año.
            if (!_cache.TryGetValue(ClaveApertura, out bool evaluada) || !evaluada)
            {
                await AplicarAperturaPorFechaAsync();
                _cache.Set(ClaveApertura, true, VigenciaCache);
            }

            // Año vigente: manda el que se activó a mano y la apertura por
            // fecha sólo decide cuando no hay ninguno marcado. Antes se tomaba
            // el mayor entre ambos y un año posterior con apertura ya cumplida
            // impedía volver a uno anterior desde Configuración.
            var anio = await _db.Database
                .SqlQueryRaw<int>(SqlVigente)
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
        if (_cache.TryGetValue(ClaveLista, out List<AnioEscolarOpcion>? enMemoria) && enMemoria is not null)
        {
            return enMemoria;
        }

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
                    "select a.anio, a.activo_matricula, a.apertura, a.cierre, "
                    + "(select count(*)::int from matricula_anual m where m.anio = a.anio) "
                    + "from anio_escolar a order by a.anio desc";

                await using var lector = await comando.ExecuteReaderAsync();

                while (await lector.ReadAsync())
                {
                    anios.Add(new AnioEscolarOpcion(
                        lector.GetInt16(0),
                        lector.GetBoolean(1),
                        lector.IsDBNull(2) ? null : DateOnly.FromDateTime(lector.GetDateTime(2)),
                        lector.IsDBNull(3) ? null : DateOnly.FromDateTime(lector.GetDateTime(3)),
                        lector.GetInt32(4)));
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

        // La pantalla debe mostrar como activo el mismo año que usa Matrícula:
        // primero el marcado a mano y, sólo si no hay ninguno, el más reciente
        // cuya apertura ya llegó.
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var marcados = anios
            .Where(opcion => opcion.Activo)
            .Select(opcion => opcion.Anio)
            .ToList();
        var vigente = marcados.Count > 0
            ? marcados.Max()
            : anios
                .Where(opcion => opcion.Apertura is DateOnly apertura && apertura <= hoy)
                .Select(opcion => opcion.Anio)
                .DefaultIfEmpty(0)
                .Max();

        var lista = anios
            .Select(opcion => opcion with { Activo = opcion.Anio == vigente })
            .ToList();

        _cache.Set(ClaveLista, lista, VigenciaCache);

        return lista;
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
    /// Deja un año marcado como único activo. Devuelve false si no existe. La
    /// activación manual manda sobre la apertura automática: el año elegido se
    /// usa aunque otro posterior ya tenga su apertura cumplida.
    /// </summary>
    public async Task<bool> ActivarAsync(int anio)
    {
        try
        {
            // Con reintentos configurados en el contexto, una transacción manual
            // debe ejecutarse dentro de la estrategia de ejecución.
            var estrategia = _db.Database.CreateExecutionStrategy();
            var activado = false;

            await estrategia.ExecuteAsync(async () =>
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

                    return;
                }

                await transaccion.CommitAsync();
                activado = true;
            });

            if (!activado)
            {
                return false;
            }
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
    /// Elimina un año registrado por error. Devuelve el error, o null si se borró.
    /// No se puede eliminar el año activo ni uno que ya tenga matrículas o
    /// certificados registrados: en esos casos devuelve el motivo sin tocar nada.
    /// </summary>
    public async Task<string?> EliminarAsync(int anio)
    {
        try
        {
            // Mismo año vigente que muestra la pantalla y usa Matrícula: el
            // activado a mano manda sobre la apertura por fecha.
            var activo = await _db.Database
                .SqlQueryRaw<int>(SqlVigente)
                .SingleAsync();

            if (activo == anio)
            {
                return $"No se puede eliminar el año {anio}: es el año activo. Active otro año primero.";
            }

            var matriculas = await _db.Database
                .SqlQueryRaw<int>(
                    "select count(*)::int as \"Value\" from matricula_anual where anio = {0}",
                    anio)
                .SingleAsync();

            if (matriculas > 0)
            {
                return $"No se puede eliminar el año {anio}: ya tiene {matriculas} matrícula(s) registrada(s).";
            }

            // La tabla de certificados es de un paso posterior de la instalación:
            // en una base antigua puede no existir y el borrado no debe romperse.
            var hayCertificados = await _db.Database
                .SqlQueryRaw<bool>("select to_regclass('certificado_emitido') is not null as \"Value\"")
                .SingleAsync();

            var certificados = hayCertificados
                ? await _db.Database
                    .SqlQueryRaw<int>(
                        "select count(*)::int as \"Value\" from certificado_emitido where anio = {0}",
                        anio)
                    .SingleAsync()
                : 0;

            if (certificados > 0)
            {
                return $"No se puede eliminar el año {anio}: ya tiene {certificados} certificado(s) registrado(s).";
            }

            var estrategia = _db.Database.CreateExecutionStrategy();
            var borrado = false;

            await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await _db.Database.BeginTransactionAsync();

                await _db.Database.ExecuteSqlRawAsync(
                    "delete from folio_secuencia where anio = {0}",
                    anio);

                var filas = await _db.Database.ExecuteSqlRawAsync(
                    "delete from anio_escolar where anio = {0}",
                    anio);

                if (filas == 0)
                {
                    await transaccion.RollbackAsync();

                    return;
                }

                await transaccion.CommitAsync();
                borrado = true;
            });

            if (!borrado)
            {
                return $"El año {anio} no está registrado.";
            }
        }
        catch (DbException ex)
        {
            _log.LogError(ex, "No se pudo eliminar el año escolar {Anio}.", anio);

            return "No se pudo eliminar el año: revise la conexión con la base de datos.";
        }

        Invalidar();

        return null;
    }

    /// <summary>
    /// Corrige un año registrado: el número y las fechas del periodo. Devuelve el
    /// error, o null si quedó actualizado. El número no se cambia cuando ya hay
    /// matrículas o certificados: esos registros quedarían con el año anterior.
    /// </summary>
    public async Task<string?> EditarAsync(int anioOriginal, int anio, DateOnly? apertura, DateOnly? cierre)
    {
        if (anio is < 2000 or > 2100)
        {
            return "El año escolar debe estar entre 2000 y 2100.";
        }

        if (apertura is not null && cierre is not null && cierre < apertura)
        {
            return "El cierre del periodo no puede ser anterior a la apertura.";
        }

        try
        {
            var registrado = await _db.Database
                .SqlQueryRaw<int>(
                    "select count(*)::int as \"Value\" from anio_escolar where anio = {0}",
                    anioOriginal)
                .SingleAsync();

            if (registrado == 0)
            {
                return $"El año {anioOriginal} no está registrado.";
            }

            if (anio != anioOriginal)
            {
                var matriculas = await _db.Database
                    .SqlQueryRaw<int>(
                        "select count(*)::int as \"Value\" from matricula_anual where anio = {0}",
                        anioOriginal)
                    .SingleAsync();

                if (matriculas > 0)
                {
                    return $"No se puede cambiar el año {anioOriginal} a {anio}: "
                        + $"ya tiene {matriculas} matrícula(s) registrada(s). Corrija sólo las fechas.";
                }

                var hayCertificados = await _db.Database
                    .SqlQueryRaw<bool>("select to_regclass('certificado_emitido') is not null as \"Value\"")
                    .SingleAsync();

                var certificados = hayCertificados
                    ? await _db.Database
                        .SqlQueryRaw<int>(
                            "select count(*)::int as \"Value\" from certificado_emitido where anio = {0}",
                            anioOriginal)
                        .SingleAsync()
                    : 0;

                if (certificados > 0)
                {
                    return $"No se puede cambiar el año {anioOriginal} a {anio}: "
                        + $"ya tiene {certificados} certificado(s) registrado(s). Corrija sólo las fechas.";
                }
            }

            // Las fechas son opcionales: se arma el UPDATE con las columnas que
            // existen para no enviar parámetros nulos sin tipo a PostgreSQL.
            var sql = "update anio_escolar set anio = {0}";
            var parametros = new List<object> { anio };

            sql += apertura is null ? ", apertura = null" : $", apertura = {{{parametros.Count}}}";
            if (apertura is not null)
            {
                parametros.Add(apertura.Value);
            }

            sql += cierre is null ? ", cierre = null" : $", cierre = {{{parametros.Count}}}";
            if (cierre is not null)
            {
                parametros.Add(cierre.Value);
            }

            sql += $", actualizado_en = now() where anio = {{{parametros.Count}}}";
            parametros.Add(anioOriginal);

            var estrategia = _db.Database.CreateExecutionStrategy();
            var editado = false;

            await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await _db.Database.BeginTransactionAsync();

                if (anio != anioOriginal)
                {
                    // El folio se emite por año: su secuencia acompaña al cambio.
                    await _db.Database.ExecuteSqlRawAsync(
                        "update folio_secuencia set anio = {0} where anio = {1}",
                        anio,
                        anioOriginal);
                }

                var filas = await _db.Database.ExecuteSqlRawAsync(sql, parametros.ToArray());

                if (filas == 0)
                {
                    await transaccion.RollbackAsync();

                    return;
                }

                await transaccion.CommitAsync();
                editado = true;
            });

            if (!editado)
            {
                return $"El año {anioOriginal} no está registrado.";
            }
        }
        catch (DbException ex) when (ex.SqlState == "23505")
        {
            return $"No se puede usar el año {anio}: ya está registrado.";
        }
        catch (DbException ex)
        {
            _log.LogError(ex, "No se pudo actualizar el año escolar {Anio}.", anioOriginal);

            return "No se pudo actualizar el año: revise la conexión con la base de datos.";
        }

        Invalidar();

        return null;
    }

    /// <summary>
    /// Apertura automática por fecha, nunca por año calendario: si el periodo de
    /// un año ya abrió, lo activa cuando todavía no hay ningún año activo. No
    /// pisa una activación manual: Configuración manda siempre.
    /// </summary>
    public async Task AplicarAperturaPorFechaAsync()
    {
        const string sql = """
            do $$
            declare
                candidato smallint;
            begin
                -- Activación manual manda: la apertura sólo elige el año
                -- cuando todavía no hay ninguno activo.
                if exists (select 1 from anio_escolar where activo_matricula) then
                    return;
                end if;

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

    /// <summary>Descarta el año activo, la lista y la evaluación de apertura.</summary>
    public void Invalidar()
    {
        _cache.Remove(ClaveActivo);
        _cache.Remove(ClaveApertura);
        _cache.Remove(ClaveLista);
    }
}
