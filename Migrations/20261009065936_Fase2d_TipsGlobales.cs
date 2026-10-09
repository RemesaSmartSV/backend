using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class Fase2d_TipsGlobales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TipsFinancieros_Categorias_IdCategoria",
                table: "TipsFinancieros");

            migrationBuilder.AlterColumn<int>(
                name: "IdCategoria",
                table: "TipsFinancieros",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // Los tips pasan a ser contenido global: borrar la categoría de un hogar ya no debe
            // llevarse el consejo educativo por delante.
            migrationBuilder.AddForeignKey(
                name: "FK_TipsFinancieros_Categorias_IdCategoria",
                table: "TipsFinancieros",
                column: "IdCategoria",
                principalTable: "Categorias",
                principalColumn: "IdCategoria",
                onDelete: ReferentialAction.SetNull);

            // #83: la tabla quedaba vacía y la página de educación financiera no mostraba nada.
            // Se siembran los dos consejos que ya existían en
            // backend/scripts/datos_prueba_demo.sql, sin inventar contenido nuevo. Quedan sin
            // categoría porque ahora son globales y las categorías son por hogar.
            migrationBuilder.Sql(
                "INSERT INTO \"TipsFinancieros\" (\"IdCategoria\", \"Titulo\", \"Contenido\") " +
                "SELECT NULL, 'Planificación de menú semanal', " +
                "'Elaborar un menú semanal antes de ir al supermercado te ayuda a comprar solo lo necesario y evitar desperdicios, reduciendo significativamente tus gastos mensuales.' " +
                "WHERE NOT EXISTS (SELECT 1 FROM \"TipsFinancieros\" t WHERE t.\"Titulo\" = 'Planificación de menú semanal');");

            migrationBuilder.Sql(
                "INSERT INTO \"TipsFinancieros\" (\"IdCategoria\", \"Titulo\", \"Contenido\") " +
                "SELECT NULL, 'Optimización del consumo eléctrico', " +
                "'Desconectar los electrodomésticos que no están en uso y aprovechar la luz natural puede disminuir hasta un 15% el monto de tu recibo de energía mensual.' " +
                "WHERE NOT EXISTS (SELECT 1 FROM \"TipsFinancieros\" t WHERE t.\"Titulo\" = 'Optimización del consumo eléctrico');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Antes de volver la FK a obligatoria hay que quitar los tips globales: rellenarlos
            // con una categoría inexistente rompería la constraint. Se borran de forma explícita
            // en lugar de asignar un valor por defecto falso.
            migrationBuilder.Sql("DELETE FROM \"TipsFinancieros\" WHERE \"IdCategoria\" IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_TipsFinancieros_Categorias_IdCategoria",
                table: "TipsFinancieros");

            migrationBuilder.AlterColumn<int>(
                name: "IdCategoria",
                table: "TipsFinancieros",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TipsFinancieros_Categorias_IdCategoria",
                table: "TipsFinancieros",
                column: "IdCategoria",
                principalTable: "Categorias",
                principalColumn: "IdCategoria",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
