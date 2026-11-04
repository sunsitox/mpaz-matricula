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
    }
}
