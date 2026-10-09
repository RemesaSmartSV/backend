using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class Fase2c_SeedCategoriasSistema : Migration
    {
        /// <summary>
        /// Lista congelada a propósito. Si mañana evoluciona CategoriasSistema.Predeterminadas,
        /// esta migración no debe cambiar de significado: representa el estado de las categorías
        /// del sistema en la FASE 2c.
        /// </summary>
        private const string ValoresCategorias =
            "('Remesas', 'Ingreso', '\uD83C\uDF10'), " +
            "('Salario', 'Ingreso', '\uD83D\uDCBC'), " +
            "('Otros ingresos', 'Ingreso', '\uD83D\uDCB0'), " +
            "('Alimentación', 'Gasto', '\uD83D\uDED2'), " +
            "('Vivienda', 'Gasto', '\uD83C\uDFE0'), " +
            "('Servicios', 'Gasto', '\uD83D\uDCA1'), " +
            "('Salud', 'Gasto', '\uD83C\uDFE5'), " +
            "('Transporte', 'Gasto', '\uD83D\uDE8C'), " +
            "('Educación', 'Gasto', '\uD83D\uDCDA'), " +
            "('Ocio', 'Gasto', '\uD83C\uDFAE')";

        /// <summary>
        /// Plegado de nombres idéntico al de CategoriasSistema.Normalizar, para no duplicar
        /// "Alimentación" cuando el hogar ya tiene "Alimentacion".
        /// </summary>
        private const string PlegarNombre = "translate(lower({0}), 'áéíóúü', 'aeiouu')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill: las categorías del sistema entran por cada hogar ya existente.
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
            // Retroceder solo retira las categorías del sistema que nadie está usando, para no
            // dejar movimientos o presupuestos huérfanos.
            migrationBuilder.Sql(
                "DELETE FROM \"Categorias\" ct " +
                "WHERE ct.\"EsSistema\" " +
                "  AND " + string.Format(PlegarNombre, "ct.\"Nombre\"") + " IN ( " +
                "      SELECT " + string.Format(PlegarNombre, "c.\"Nombre\"") + " " +
                "      FROM (VALUES " + ValoresCategorias + ") AS c(\"Nombre\", \"Tipo\", \"Icono\")) " +
                "  AND NOT EXISTS (SELECT 1 FROM \"Movimientos\" m WHERE m.\"IdCategoria\" = ct.\"IdCategoria\") " +
                "  AND NOT EXISTS (SELECT 1 FROM \"Presupuestos\" p WHERE p.\"IdCategoria\" = ct.\"IdCategoria\");");
        }
    }
}
