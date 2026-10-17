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
        // Se fija RESTRICT: eliminar un hogar no debe eliminar en cascada a sus
        // miembros ni a sus datos (requisito §6.3 Caso A: los datos se conservan).
        modelBuilder.Entity<Usuario>()
            .HasOne(u => u.Hogar)
            .WithMany(h => h.Usuarios)
            .HasForeignKey(u => u.IdHogar)
            .OnDelete(DeleteBehavior.Restrict);

        // Requisito §6.3 Caso A: los datos financieros nunca se borran en cascada
        // al eliminar o abandonar una familia. RESTRICT es la red de seguridad a
        // nivel de base de datos: impide borrar un hogar mientras tenga registros.
        modelBuilder.Entity<Movimiento>()
            .HasOne(m => m.Hogar)
            .WithMany()
            .HasForeignKey(m => m.IdHogar)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MetaAhorro>()
            .HasOne(m => m.Hogar)
            .WithMany(h => h.MetasAhorro)
            .HasForeignKey(m => m.IdHogar)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AporteMeta>()
            .HasOne(a => a.Hogar)
            .WithMany()
            .HasForeignKey(a => a.IdHogar)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Presupuesto>()
            .HasOne(p => p.Hogar)
            .WithMany(h => h.Presupuestos)
            .HasForeignKey(p => p.IdHogar)
            .OnDelete(DeleteBehavior.Restrict);

        // No se puede eliminar un usuario que todavía tenga movimientos registrados.
        modelBuilder.Entity<Movimiento>()
            .HasOne(m => m.Usuario)
            .WithMany()
            .HasForeignKey(m => m.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        // Las categorías nacen con el hogar y no tienen valor propio: se mantienen
        // en cascada para que borrar un hogar sin datos financieros las limpie.
        modelBuilder.Entity<Categoria>()
            .HasOne(c => c.Hogar)
            .WithMany(h => h.Categorias)
            .HasForeignKey(c => c.IdHogar)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.GoogleSubject)
            .IsUnique();

        // Unicidad de correo: sin este indice dos cuentas podrian compartir el mismo
        // correo y el login quedaria ambiguo (una consulta devolveria dos usuarios).
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Correo)
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

        // Solo las invitaciones pendientes compiten por la unicidad (mismo patron
        // que SolicitudUnion): al rechazar, aceptar o cancelar se puede volver a
        // invitar al mismo correo en el mismo hogar sin chocar con el indice.
        modelBuilder.Entity<InvitacionFamilia>()
            .HasIndex(i => new { i.IdHogar, i.Correo })
            .IsUnique()
            .HasFilter("\"Estado\" = 'Pendiente'");

        // Un presupuesto por hogar, categoria y mes: evita que un doble submit
        // cree dos limites para la misma categoria en el mismo periodo.
        modelBuilder.Entity<Presupuesto>()
            .HasIndex(p => new { p.IdHogar, p.IdCategoria, p.MesAnio })
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
