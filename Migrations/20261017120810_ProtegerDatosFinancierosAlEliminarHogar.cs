using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class ProtegerDatosFinancierosAlEliminarHogar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Aportes_Hogares_IdHogar",
                table: "Aportes");

            migrationBuilder.DropForeignKey(
                name: "FK_MetasAhorro_Hogares_IdHogar",
                table: "MetasAhorro");

            migrationBuilder.DropForeignKey(
                name: "FK_Movimientos_Hogares_IdHogar",
                table: "Movimientos");

            migrationBuilder.DropForeignKey(
                name: "FK_Movimientos_Usuarios_IdUsuario",
                table: "Movimientos");

            migrationBuilder.DropForeignKey(
                name: "FK_Presupuestos_Hogares_IdHogar",
                table: "Presupuestos");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Hogares_IdHogar",
                table: "Usuarios");

            migrationBuilder.AddForeignKey(
                name: "FK_Aportes_Hogares_IdHogar",
                table: "Aportes",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MetasAhorro_Hogares_IdHogar",
                table: "MetasAhorro",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Movimientos_Hogares_IdHogar",
                table: "Movimientos",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Movimientos_Usuarios_IdUsuario",
                table: "Movimientos",
                column: "IdUsuario",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Presupuestos_Hogares_IdHogar",
                table: "Presupuestos",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Hogares_IdHogar",
                table: "Usuarios",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Aportes_Hogares_IdHogar",
                table: "Aportes");

            migrationBuilder.DropForeignKey(
                name: "FK_MetasAhorro_Hogares_IdHogar",
                table: "MetasAhorro");

            migrationBuilder.DropForeignKey(
                name: "FK_Movimientos_Hogares_IdHogar",
                table: "Movimientos");

            migrationBuilder.DropForeignKey(
                name: "FK_Movimientos_Usuarios_IdUsuario",
                table: "Movimientos");

            migrationBuilder.DropForeignKey(
                name: "FK_Presupuestos_Hogares_IdHogar",
                table: "Presupuestos");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Hogares_IdHogar",
                table: "Usuarios");

            migrationBuilder.AddForeignKey(
                name: "FK_Aportes_Hogares_IdHogar",
                table: "Aportes",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MetasAhorro_Hogares_IdHogar",
                table: "MetasAhorro",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Movimientos_Hogares_IdHogar",
                table: "Movimientos",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Movimientos_Usuarios_IdUsuario",
                table: "Movimientos",
                column: "IdUsuario",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Presupuestos_Hogares_IdHogar",
                table: "Presupuestos",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Hogares_IdHogar",
                table: "Usuarios",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
