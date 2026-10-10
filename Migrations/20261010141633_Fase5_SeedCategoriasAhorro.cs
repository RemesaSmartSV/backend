using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class Fase5_SeedCategoriasAhorro : Migration
    {
        /// <summary>
        /// Lista congelada a propósito: son las 7 categorías tipo Ahorro del
        /// prompt maestro §12. "Vivienda propia" y "Educación futura" llevan
        /// sufijos para no colisionar con las categorías de Gasto homónimas
        /// (el plegado de nombres las descartaría por duplicado).
        /// </summary>
        private const string ValoresCategorias =
            "('Fondo de emergencia', 'Ahorro', '\uD83C\uDDF8'), " +
            "('Vivienda propia', 'Ahorro', '\uD83C\uDFE1'), " +
            "('Educación futura', 'Ahorro', '\uD83C\uDF93'), " +
            "('Vehículo', 'Ahorro', '\uD83D\uDE97'), " +
            "('Viajes', 'Ahorro', '\u2708\uFE0F'), " +
            "('Tecnología', 'Ahorro', '\uD83D\uDCBF'), " +
            "('Otros ahorros', 'Ahorro', '\uD83D\uDC37')";

        /// <summary>
        /// Plegado de nombres idéntico al de CategoriasSistema.Normalizar.
        /// </summary>
        private const string PlegarNombre = "translate(lower({0}), 'áéíóúü', 'aeiouu')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill: las 7 categorías de ahorro entran por cada hogar existente.
            // Idempotente gracias al NOT EXISTS (plegado de nombres).
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
            // Retroceder solo retira las categorías de ahorro que nadie esté usando.
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
