using MarcelaPaz.Domain.Entidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Infrastructure.Persistence;

/// <summary>
/// Contexto único del portal.
///
/// Las tablas de identidad (contraseñas en hash) viven en el esquema <c>auth</c> y
/// las administra EF Core con migraciones. Las tablas del DDL institucional viven en
/// <c>public</c> y quedan excluidas de las migraciones: su fuente de verdad es el
/// script SQL entregado por el colegio, aquí solo se mapean.
/// </summary>
public class MatriculaDbContext : IdentityDbContext<IdentityUser>
{
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

    public DbSet<RestriccionFamiliar> RestriccionesFamiliares => Set<RestriccionFamiliar>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity queda aislado en su propio esquema para no mezclarse con el DDL.
        foreach (var entidad in builder.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true))
        {
            entidad.SetSchema("auth");
        }

        builder.Entity<Funcionario>(e =>
        {
            e.ToTable("funcionario", "public", t => t.ExcludeFromMigrations());
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
            e.ToTable("rol", "public", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(30).IsRequired();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        builder.Entity<FuncionarioRol>(e =>
        {
            e.ToTable("funcionario_rol", "public", t => t.ExcludeFromMigrations());
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
            e.ToTable("rol_permiso", "public", t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.RolId, x.Area, x.Accion });
            e.Property(x => x.RolId).HasColumnName("rol_id");
            e.Property(x => x.Area).HasColumnName("area").HasMaxLength(30);
            e.Property(x => x.Accion).HasColumnName("accion").HasMaxLength(15);
        });

        builder.Entity<RegistroAcceso>(e =>
        {
            e.ToTable("registros_acceso", "public", t => t.ExcludeFromMigrations());
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
            e.ToTable("estudiante", "public", t => t.ExcludeFromMigrations());
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
            e.ToTable("matricula_anual", "public", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.Anio).HasColumnName("anio");
            e.Property(x => x.CursoId).HasColumnName("curso_id");
            e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
            e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Curso).WithMany().HasForeignKey(x => x.CursoId);
        });

        builder.Entity<Curso>(e =>
        {
            e.ToTable("curso", "public", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(30).IsRequired();
            e.Property(x => x.Activo).HasColumnName("activo");
        });

        builder.Entity<Comuna>(e =>
        {
            e.ToTable("comuna", "public", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(80).IsRequired();
        });

        // Personas vinculadas al estudiante (madre, padre, apoderados).
        builder.Entity<Persona>(e =>
        {
            e.ToTable("persona", "public", t => t.ExcludeFromMigrations());
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
            e.ToTable("estudiante_persona", "public", t => t.ExcludeFromMigrations());
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
            e.ToTable("contacto_emergencia", "public", t => t.ExcludeFromMigrations());
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
            e.ToTable("retiro_autorizado", "public", t => t.ExcludeFromMigrations());
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
            e.ToTable("situacion_medica", "public", t => t.ExcludeFromMigrations());
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
            e.ToTable("antecedente_salud", "public", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudianteId).HasColumnName("estudiante_id");
            e.Property(x => x.TieneSituacionMedica).HasColumnName("tiene_situacion_medica");
            e.Property(x => x.NecesidadesAtencion).HasColumnName("necesidades_atencion");
        });

        builder.Entity<RestriccionFamiliar>(e =>
        {
            e.ToTable("restriccion_familiar", "public", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EstudiantePersonaId).HasColumnName("estudiante_persona_id");
            e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
            e.Property(x => x.Descripcion).HasColumnName("descripcion").IsRequired();
            e.Property(x => x.VigenteDesde).HasColumnName("vigente_desde");
            e.Property(x => x.VigenteHasta).HasColumnName("vigente_hasta");
            e.HasOne(x => x.EstudiantePersona).WithMany().HasForeignKey(x => x.EstudiantePersonaId);
        });
    }
}
