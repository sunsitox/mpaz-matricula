using MarcelaPaz.Domain.Entidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Infrastructure.Persistence;

/// <summary>
/// Contexto único del portal.
///
/// Todo el portal vive en el esquema <see cref="Esquema"/>: las tablas del DDL
/// institucional y las de identidad (contraseñas en hash). Así esta base puede
/// convivir con otras bases del colegio sin mezclar nombres de tablas.
/// Las tablas institucionales quedan excluidas de las migraciones: su fuente de
/// verdad es el script SQL, aquí solo se mapean.
/// </summary>
public class MatriculaDbContext : IdentityDbContext<IdentityUser>
{
    /// <summary>Esquema PostgreSQL exclusivo del portal.</summary>
    public const string Esquema = "mpaz_matricula";

    public MatriculaDbContext(DbContextOptions<MatriculaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();

    public DbSet<Rol> Roles => Set<Rol>();

    public DbSet<FuncionarioRol> FuncionarioRoles => Set<FuncionarioRol>();

    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();

    public DbSet<RegistroAcceso> RegistrosAcceso => Set<RegistroAcceso>();

    public DbSet<Estudiante> Estudiantes => Set<Estudiante>();

    public DbSet<MatriculaAnual> Matriculas => Set<MatriculaAnual>();

    public DbSet<Curso> Cursos => Set<Curso>();

    public DbSet<Comuna> Comunas => Set<Comuna>();

    public DbSet<Persona> Personas => Set<Persona>();

    public DbSet<EstudiantePersona> EstudiantesPersonas => Set<EstudiantePersona>();

    public DbSet<ContactoEmergencia> ContactosEmergencia => Set<ContactoEmergencia>();

    public DbSet<RetiroAutorizado> RetirosAutorizados => Set<RetiroAutorizado>();

    public DbSet<SituacionMedica> SituacionesMedicas => Set<SituacionMedica>();

    public DbSet<AntecedenteSalud> AntecedentesSalud => Set<AntecedenteSalud>();

    public DbSet<AntecedenteApoyo> AntecedentesApoyo => Set<AntecedenteApoyo>();

    public DbSet<RestriccionFamiliar> RestriccionesFamiliares => Set<RestriccionFamiliar>();

    public DbSet<FichaVersion> FichaVersiones => Set<FichaVersion>();

    public DbSet<PlantillaAutorizacion> PlantillasAutorizacion => Set<PlantillaAutorizacion>();

    public DbSet<Autorizacion> Autorizaciones => Set<Autorizacion>();

    public DbSet<AutorizacionAlcance> AutorizacionesAlcance => Set<AutorizacionAlcance>();

    public DbSet<Documento> Documentos => Set<Documento>();

    public DbSet<RequisitoDocumental> RequisitosDocumentales => Set<RequisitoDocumental>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Esquema por defecto: también ubica __EFMigrationsHistory.
        builder.HasDefaultSchema(Esquema);

        // Identity comparte el esquema del portal (no se usa "auth" genérico).
        foreach (var entidad in builder.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true))
        {
            entidad.SetSchema(Esquema);
        }

        builder.Entity<Funcionario>(e =>
        {
            e.ToTable("funcionario", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
            e.Property(x => x.Correo).HasColumnName("correo").HasMaxLength(255).IsRequired();
            e.Property(x => x.SsoSujeto).HasColumnName("sso_sujeto").HasMaxLength(255);
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.HasIndex(x => x.Correo).IsUnique();
            e.HasIndex(x => x.SsoSujeto).IsUnique();
        });

        builder.Entity<Rol>(e =>
        {
            e.ToTable("rol", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(30).IsRequired();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        builder.Entity<FuncionarioRol>(e =>
        {
            e.ToTable("funcionario_rol", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.FuncionarioId, x.RolId });
            e.Property(x => x.FuncionarioId).HasColumnName("funcionario_id");
            e.Property(x => x.RolId).HasColumnName("rol_id");
            e.Property(x => x.Desde).HasColumnName("desde");
            e.Property(x => x.Hasta).HasColumnName("hasta");
            e.HasOne(x => x.Funcionario)
                .WithMany(f => f.Roles)
                .HasForeignKey(x => x.FuncionarioId);
            e.HasOne(x => x.Rol)
                .WithMany()
                .HasForeignKey(x => x.RolId);
        });

        builder.Entity<RolPermiso>(e =>
        {
            e.ToTable("rol_permiso", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.RolId, x.Area, x.Accion });
            e.Property(x => x.RolId).HasColumnName("rol_id");
            e.Property(x => x.Area).HasColumnName("area").HasMaxLength(30);
            e.Property(x => x.Accion).HasColumnName("accion").HasMaxLength(15);
        });

        builder.Entity<RegistroAcceso>(e =>
        {
            e.ToTable("registros_acceso", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FuncionarioId).HasColumnName("funcionario_id");
            e.Property(x => x.Accion).HasColumnName("accion").HasMaxLength(12).IsRequired();
            e.Property(x => x.Area).HasColumnName("area").HasMaxLength(30);
            e.Property(x => x.Recurso).HasColumnName("recurso").HasMaxLength(63);
            e.Property(x => x.RecursoId).HasColumnName("recurso_id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.Detalle).HasColumnName("detalle").HasColumnType("jsonb");
            e.Property(x => x.Ip).HasColumnName("ip");
            e.Property(x => x.OcurridoEn).HasColumnName("ocurrido_en").HasDefaultValueSql("now()");
        });

        // Registro único de estudiantes: sólo lectura para la ficha.
        builder.Entity<Estudiante>(e =>
        {
            e.ToTable("estudiante", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TipoIdentificador).HasColumnName("tipo_identificador").HasMaxLength(10).IsRequired();
            e.Property(x => x.Identificador).HasColumnName("identificador").HasMaxLength(20).IsRequired();
            e.Property(x => x.Nombres).HasColumnName("nombres").HasMaxLength(120).IsRequired();
            e.Property(x => x.ApellidoPaterno).HasColumnName("apellido_paterno").HasMaxLength(80).IsRequired();
            e.Property(x => x.ApellidoMaterno).HasColumnName("apellido_materno").HasMaxLength(80);
            e.Property(x => x.FechaNacimiento).HasColumnName("fecha_nacimiento");
            e.Property(x => x.Nacionalidad).HasColumnName("nacionalidad").HasMaxLength(60).IsRequired();
            e.Property(x => x.Domicilio).HasColumnName("domicilio").HasMaxLength(255).IsRequired();
            e.Property(x => x.ComunaId).HasColumnName("comuna_id");
            e.Property(x => x.SaleSoloFinalJornada).HasColumnName("sale_solo_final_jornada");
            e.HasOne(x => x.Comuna).WithMany().HasForeignKey(x => x.ComunaId);
        });

        // Historial de matrículas: cada año su propio registro.
        builder.Entity<MatriculaAnual>(e =>
        {
            e.ToTable("matricula_anual", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.Anio).HasColumnName("anio");
            e.Property(x => x.CursoId).HasColumnName("curso_id");
            e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
            e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
            e.Property(x => x.CreadaPor).HasColumnName("creada_por");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            e.Property(x => x.ConfirmadaEn).HasColumnName("confirmada_en");
            e.Property(x => x.ConfirmadaPor).HasColumnName("confirmada_por");
            e.HasOne(x => x.Curso).WithMany().HasForeignKey(x => x.CursoId);
        });

        builder.Entity<Curso>(e =>
        {
            e.ToTable("curso", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(30).IsRequired();
            e.Property(x => x.Activo).HasColumnName("activo");
        });

        builder.Entity<Comuna>(e =>
        {
            e.ToTable("comuna", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(80).IsRequired();
        });

        // Personas vinculadas al estudiante (madre, padre, apoderados).
        builder.Entity<Persona>(e =>
        {
            e.ToTable("persona", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TipoIdentificador).HasColumnName("tipo_identificador").HasMaxLength(10).IsRequired();
            e.Property(x => x.Identificador).HasColumnName("identificador").HasMaxLength(20).IsRequired();
            e.Property(x => x.Nombres).HasColumnName("nombres").HasMaxLength(120).IsRequired();
            e.Property(x => x.Apellidos).HasColumnName("apellidos").HasMaxLength(120).IsRequired();
            e.Property(x => x.Telefono).HasColumnName("telefono").HasMaxLength(30);
            e.Property(x => x.Correo).HasColumnName("correo").HasMaxLength(120);
            e.Property(x => x.Domicilio).HasColumnName("domicilio").HasMaxLength(255);
            e.Property(x => x.Escolaridad).HasColumnName("escolaridad").HasMaxLength(80);
        });

        builder.Entity<EstudiantePersona>(e =>
        {
            e.ToTable("estudiante_persona", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.PersonaId).HasColumnName("persona_id");
            e.Property(x => x.Parentesco).HasColumnName("parentesco").HasMaxLength(20).IsRequired();
            e.Property(x => x.Vinculo).HasColumnName("vinculo").HasMaxLength(60);
            e.Property(x => x.TipoApoderado).HasColumnName("tipo_apoderado").HasMaxLength(15);
            e.Property(x => x.TieneRestriccion).HasColumnName("tiene_restriccion");
            e.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
            e.Property(x => x.VigenteHasta).HasColumnName("vigente_hasta");
            e.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId);
            e.HasOne(x => x.Estudiante).WithMany().HasForeignKey(x => x.EstudianteId);
        });

        builder.Entity<ContactoEmergencia>(e =>
        {
            e.ToTable("contacto_emergencia", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.Orden).HasColumnName("orden");
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
            e.Property(x => x.Telefono).HasColumnName("telefono").HasMaxLength(30).IsRequired();
            e.Property(x => x.Vinculo).HasColumnName("vinculo").HasMaxLength(60).IsRequired();
            e.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
            e.Property(x => x.VigenteHasta).HasColumnName("vigente_hasta");
        });

        builder.Entity<RetiroAutorizado>(e =>
        {
            e.ToTable("retiro_autorizado", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
            e.Property(x => x.TipoIdentificador).HasColumnName("tipo_identificador").HasMaxLength(10).IsRequired();
            e.Property(x => x.Identificador).HasColumnName("identificador").HasMaxLength(20).IsRequired();
            e.Property(x => x.Vinculo).HasColumnName("vinculo").HasMaxLength(60).IsRequired();
            e.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
            e.Property(x => x.VigenteHasta).HasColumnName("vigente_hasta");
        });

        builder.Entity<SituacionMedica>(e =>
        {
            e.ToTable("situacion_medica", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
            e.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(255).IsRequired();
            e.Property(x => x.EnTratamiento).HasColumnName("en_tratamiento");
            e.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
            e.Property(x => x.VigenteHasta).HasColumnName("vigente_hasta");
        });

        builder.Entity<AntecedenteSalud>(e =>
        {
            e.ToTable("antecedente_salud", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.TieneSituacionMedica).HasColumnName("tiene_situacion_medica");
            e.Property(x => x.NecesidadesAtencion).HasColumnName("necesidades_atencion");
            e.Property(x => x.ContactoSanitarioNombre).HasColumnName("contacto_sanitario_nombre").HasMaxLength(150);
            e.Property(x => x.ContactoSanitarioTelefono).HasColumnName("contacto_sanitario_telefono").HasMaxLength(20);
        });

        builder.Entity<AntecedenteApoyo>(e =>
        {
            e.ToTable("antecedente_apoyo", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.ParticipaPie).HasColumnName("participa_pie").HasMaxLength(10).IsRequired();
            e.Property(x => x.ApoyosInformados).HasColumnName("apoyos_informados");
            e.Property(x => x.TieneDiagnostico).HasColumnName("tiene_diagnostico");
        });

        builder.Entity<RestriccionFamiliar>(e =>
        {
            e.ToTable("restriccion_familiar", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudiantePersonaId).HasColumnName("estudiante_persona_id");
            e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
            e.Property(x => x.Descripcion).HasColumnName("descripcion").IsRequired();
            e.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
            e.Property(x => x.VigenteHasta).HasColumnName("vigente_hasta");
            e.HasOne(x => x.EstudiantePersona).WithMany().HasForeignKey(x => x.EstudiantePersonaId);
        });

        // Versiones cerradas de la ficha: folio y copia congelada de los datos.
        builder.Entity<FichaVersion>(e =>
        {
            e.ToTable("ficha_version", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.MatriculaId).HasColumnName("matricula_id");
            e.Property(x => x.NumeroVersion).HasColumnName("numero_version");
            e.Property(x => x.Folio).HasColumnName("folio").HasMaxLength(20).IsRequired();
            e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(12).IsRequired();
            e.Property(x => x.DatosSnapshot).HasColumnName("datos_snapshot").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.GeneradaPor).HasColumnName("generada_por");
            e.Property(x => x.GeneradaEn).HasColumnName("generada_en");
            e.Property(x => x.PdfDocumentoId).HasColumnName("pdf_documento_id");
            e.Property(x => x.FechaFirma).HasColumnName("fecha_firma");
            e.Property(x => x.FirmantePersonaId).HasColumnName("firmante_persona_id");
        });

        builder.Entity<Documento>(e =>
        {
            e.ToTable("documento", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.MatriculaId).HasColumnName("matricula_id");
            e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(30).IsRequired();
            e.Property(x => x.Sensibilidad).HasColumnName("sensibilidad").HasMaxLength(10).IsRequired();
            e.Property(x => x.NombreOriginal).HasColumnName("nombre_original").HasMaxLength(255).IsRequired();
            e.Property(x => x.MimeType).HasColumnName("mime_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.TamanoBytes).HasColumnName("tamano_bytes");
            e.Property(x => x.RutaAlmacenamiento).HasColumnName("ruta_almacenamiento").IsRequired();
            e.Property(x => x.HashSha256).HasColumnName("hash_sha256").HasMaxLength(64).IsRequired();
            e.Property(x => x.EstadoAnalisis).HasColumnName("estado_analisis").HasMaxLength(10).IsRequired();
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.ReemplazaId).HasColumnName("reemplaza_id");
            e.Property(x => x.SubidoPor).HasColumnName("subido_por");
            e.Property(x => x.SubidoEn).HasColumnName("subido_en");
        });

        builder.Entity<RequisitoDocumental>(e =>
        {
            e.ToTable("requisito_documental", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.MatriculaId).HasColumnName("matricula_id");
            e.Property(x => x.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(30).IsRequired();
            e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(10).IsRequired();
            e.Property(x => x.DocumentoId).HasColumnName("documento_id");
            e.Property(x => x.Observacion).HasColumnName("observacion");
            e.Property(x => x.RegistradoPor).HasColumnName("registrado_por");
            e.Property(x => x.RegistradoEn).HasColumnName("registrado_en");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.Version).HasColumnName("version");
        });

        builder.Entity<PlantillaAutorizacion>(e =>
        {
            e.ToTable("plantilla_autorizacion", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(15).IsRequired();
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.Texto).HasColumnName("texto").IsRequired();
            e.Property(x => x.AprobadoPor).HasColumnName("aprobado_por").HasMaxLength(150);
            e.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
            e.Property(x => x.VigenteHasta).HasColumnName("vigente_hasta");
        });

        builder.Entity<Autorizacion>(e =>
        {
            e.ToTable("autorizacion", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(15).IsRequired();
            e.Property(x => x.PlantillaId).HasColumnName("plantilla_id");
            e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(12).IsRequired();
            e.Property(x => x.Proposito).HasColumnName("proposito");
            e.Property(x => x.FichaVersionId).HasColumnName("ficha_version_id");
            e.Property(x => x.FirmantePersonaId).HasColumnName("firmante_persona_id");
            e.Property(x => x.FechaFirma).HasColumnName("fecha_firma");
            e.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
            e.Property(x => x.VigenteHasta).HasColumnName("vigente_hasta");
            e.Property(x => x.RevocadaEn).HasColumnName("revocada_en");
            e.Property(x => x.MotivoRevocacion).HasColumnName("motivo_revocacion");
            e.HasMany(x => x.Alcances).WithOne().HasForeignKey(a => a.AutorizacionId);
        });

        builder.Entity<AutorizacionAlcance>(e =>
        {
            e.ToTable("autorizacion_alcance", Esquema, t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AutorizacionId).HasColumnName("autorizacion_id");
            e.Property(x => x.Alcance).HasColumnName("alcance").HasMaxLength(60).IsRequired();
            e.Property(x => x.Otorgado).HasColumnName("otorgado");
        });
    }
}
