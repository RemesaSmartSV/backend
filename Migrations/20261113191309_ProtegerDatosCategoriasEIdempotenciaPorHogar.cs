using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class ProtegerDatosCategoriasEIdempotenciaPorHogar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Movimientos_Categorias_IdCategoria",
                table: "Movimientos");

            migrationBuilder.DropForeignKey(
                name: "FK_Presupuestos_Categorias_IdCategoria",
                table: "Presupuestos");

            migrationBuilder.DropIndex(
                name: "IX_Aportes_IdempotencyKey",
                table: "Aportes");

            migrationBuilder.DropIndex(
                name: "IX_Aportes_IdHogar",
                table: "Aportes");

            migrationBuilder.CreateIndex(
                name: "IX_Aportes_IdHogar_IdempotencyKey",
                table: "Aportes",
                columns: new[] { "IdHogar", "IdempotencyKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Movimientos_Categorias_IdCategoria",
                table: "Movimientos",
                column: "IdCategoria",
                principalTable: "Categorias",
                principalColumn: "IdCategoria",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Presupuestos_Categorias_IdCategoria",
                table: "Presupuestos",
                column: "IdCategoria",
                principalTable: "Categorias",
                principalColumn: "IdCategoria",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Movimientos_Categorias_IdCategoria",
                table: "Movimientos");

            migrationBuilder.DropForeignKey(
                name: "FK_Presupuestos_Categorias_IdCategoria",
                table: "Presupuestos");

            migrationBuilder.DropIndex(
                name: "IX_Aportes_IdHogar_IdempotencyKey",
                table: "Aportes");

            migrationBuilder.CreateIndex(
                name: "IX_Aportes_IdempotencyKey",
                table: "Aportes",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aportes_IdHogar",
                table: "Aportes",
                column: "IdHogar");

            migrationBuilder.AddForeignKey(
                name: "FK_Movimientos_Categorias_IdCategoria",
                table: "Movimientos",
                column: "IdCategoria",
                principalTable: "Categorias",
                principalColumn: "IdCategoria",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Presupuestos_Categorias_IdCategoria",
                table: "Presupuestos",
                column: "IdCategoria",
                principalTable: "Categorias",
                principalColumn: "IdCategoria",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
