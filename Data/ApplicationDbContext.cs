using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using RemesaSmartSV.Entities;

namespace RemesaSmartSV.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Hogar> Hogares { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Movimiento> Movimientos { get; set; }
    public DbSet<Presupuesto> Presupuestos { get; set; }
    public DbSet<MetaAhorro> MetasAhorro { get; set; }
    public DbSet<AporteMeta> Aportes { get; set; }
    public DbSet<EducacionFinanciera> TipsFinancieros { get; set; }
    public DbSet<InvitacionFamilia> InvitacionesFamilia { get; set; }
    public DbSet<SolicitudUnion> SolicitudesUnion { get; set; }
    public DbSet<Notificacion> Notificaciones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Usuario.IdHogar es opcional (registro sin familia, sección 12/13 del prompt).
        // Se fija CASCADE explícito para NO cambiar el comportamiento previo
        // (EF por convención pondría ClientSetNull al volverse opcional la relación).
        modelBuilder.Entity<Usuario>()
            .HasOne(u => u.Hogar)
            .WithMany(h => h.Usuarios)
            .HasForeignKey(u => u.IdHogar)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.GoogleSubject)
            .IsUnique();

        modelBuilder.Entity<Hogar>()
            .HasIndex(h => h.CodigoPublico)
            .IsUnique();

        modelBuilder.Entity<AporteMeta>()
            .HasOne(a => a.Usuario)
            .WithMany()
            .HasForeignKey(a => a.IdUsuario)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<MetaAhorro>()
            .HasOne(m => m.Usuario)
            .WithMany()
            .HasForeignKey(m => m.IdUsuario)
            .OnDelete(DeleteBehavior.SetNull);

        // Los tips son globales; al desvincularse de la categoría no deben borrarse con ella.
        // Antes la relación era obligatoria y CASCADE, así que esto cambia el comportamiento
        // a propósito como parte de la decisión de modelo de TipsFinancieros.
        modelBuilder.Entity<EducacionFinanciera>()
            .HasOne(t => t.Categoria)
            .WithMany()
            .HasForeignKey(t => t.IdCategoria)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InvitacionFamilia>()
            .HasOne(i => i.Invitado)
            .WithMany()
            .HasForeignKey(i => i.IdInvitado)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InvitacionFamilia>()
            .HasIndex(i => new { i.IdHogar, i.Correo })
            .IsUnique();

        modelBuilder.Entity<SolicitudUnion>()
            .HasOne(s => s.Hogar)
            .WithMany()
            .HasForeignKey(s => s.IdHogar)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SolicitudUnion>()
            .HasOne(s => s.Usuario)
            .WithMany()
            .HasForeignKey(s => s.IdUsuario)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SolicitudUnion>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(s => s.IdAdminRespuesta)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SolicitudUnion>()
            .HasIndex(s => new { s.IdHogar, s.IdUsuario })
            .IsUnique()
            .HasFilter("\"Estado\" = 'Pendiente'");

        modelBuilder.Entity<Notificacion>()
            .HasIndex(n => new { n.IdUsuario, n.Leida });

        modelBuilder.Entity<AporteMeta>()
            .HasIndex(a => a.IdempotencyKey)
            .IsUnique();

    }
}

// Fábrica en tiempo de diseño para que la CLI de EF Core se conecte directamente
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<ApplicationDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
