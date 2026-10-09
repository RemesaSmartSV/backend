using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class Fase4_SeedCategoriasAmpliadas : Migration
    {
        /// <summary>
        /// Lista congelada a propósito: son solo las categorías nuevas de la M4
        /// (ampliación del prompt maestro §11). Las diez de Fase2c ya entraron
        /// con su propia migración; esta no cambia de significado pase lo que
        /// pase con CategoriasSistema.Predeterminadas.
        /// </summary>
        private const string ValoresCategorias =
            "('Negocio', 'Ingreso', '\uD83C\uDFE2'), " +
            "('Freelance', 'Ingreso', '\uD83D\uDCBB'), " +
            "('Inversión', 'Ingreso', '\uD83D\uDCC8'), " +
            "('Compras', 'Gasto', '\uD83D\uDECD'), " +
            "('Deudas', 'Gasto', '\uD83D\uDCB3'), " +
            "('Otros gastos', 'Gasto', '\uD83E\uDDFE')";

        /// <summary>
        /// Plegado de nombres idéntico al de CategoriasSistema.Normalizar, para no
        /// duplicar "Inversión" cuando el hogar ya tiene "Inversion" (o una
        /// categoría propia con ese nombre).
        /// </summary>
        private const string PlegarNombre = "translate(lower({0}), 'áéíóúü', 'aeiouu')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill: las seis categorías nuevas entran por cada hogar existente.
            // Es idempotente gracias al NOT EXISTS, así que correrla dos veces es inocuo.
            migrationBuilder.Sql(
                "INSERT INTO \"Categorias\" (\"IdHogar\", \"Nombre\", \"Tipo\", \"Icono\", \"EsSistema\") " +
                "SELECT h.\"IdHogar\", c.\"Nombre\", c.\"Tipo\", c.\"Icono\", TRUE " +
                "FROM \"Hogares\" h " +
                "CROSS JOIN (VALUES " + ValoresCategorias + ") AS c(\"Nombre\", \"Tipo\", \"Icono\") " +
                "WHERE NOT EXISTS ( " +
                "    SELECT 1 FROM \"Categorias\" x " +
                "    WHERE x.\"IdHogar\" = h.\"IdHogar\" " +
                "      AND " + string.Format(PlegarNombre, "x.\"Nombre\"") + " " +
                "        = " + string.Format(PlegarNombre, "c.\"Nombre\"") + " " +
                ");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retroceder solo retira las categorías nuevas que nadie está usando, para no
            // dejar movimientos huérfanos.
            migrationBuilder.Sql(
                "DELETE FROM \"Categorias\" ct " +
                "WHERE ct.\"EsSistema\" " +
                "  AND " + string.Format(PlegarNombre, "ct.\"Nombre\"") + " IN ( " +
                "      SELECT " + string.Format(PlegarNombre, "c.\"Nombre\"") + " " +
                "      FROM (VALUES " + ValoresCategorias + ") AS c(\"Nombre\", \"Tipo\", \"Icono\")) " +
                "  AND NOT EXISTS ( " +
                "      SELECT 1 FROM \"Movimientos\" m WHERE m.\"IdCategoria\" = ct.\"IdCategoria\") " +
                "  AND NOT EXISTS ( " +
                "      SELECT 1 FROM \"Presupuestos\" p WHERE p.\"IdCategoria\" = ct.\"IdCategoria\");");
        }
    }
}
