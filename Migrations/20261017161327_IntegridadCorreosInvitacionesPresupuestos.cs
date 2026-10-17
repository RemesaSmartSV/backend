using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class IntegridadCorreosInvitacionesPresupuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deduplicación previa a IX_Presupuestos_IdHogar_IdCategoria_MesAnio:
            // los dobles submits podían dejar dos presupuestos para el mismo hogar,
            // categoría y mes. Se conserva el de mayor IdPresupuesto (el más reciente)
            // y se eliminan los demás para que el índice unique pueda crearse.
            migrationBuilder.Sql("""
                DELETE FROM "Presupuestos" p
                USING "Presupuestos" d
                WHERE p."IdHogar" = d."IdHogar"
                  AND p."IdCategoria" = d."IdCategoria"
                  AND p."MesAnio" = d."MesAnio"
                  AND p."IdPresupuesto" < d."IdPresupuesto";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Presupuestos_IdHogar",
                table: "Presupuestos");

            migrationBuilder.DropIndex(
                name: "IX_InvitacionesFamilia_IdHogar_Correo",
                table: "InvitacionesFamilia");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_IdHogar_IdCategoria_MesAnio",
                table: "Presupuestos",
                columns: new[] { "IdHogar", "IdCategoria", "MesAnio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesFamilia_IdHogar_Correo",
                table: "InvitacionesFamilia",
                columns: new[] { "IdHogar", "Correo" },
                unique: true,
                filter: "\"Estado\" = 'Pendiente'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Presupuestos_IdHogar_IdCategoria_MesAnio",
                table: "Presupuestos");

            migrationBuilder.DropIndex(
                name: "IX_InvitacionesFamilia_IdHogar_Correo",
                table: "InvitacionesFamilia");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_IdHogar",
                table: "Presupuestos",
                column: "IdHogar");

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesFamilia_IdHogar_Correo",
                table: "InvitacionesFamilia",
                columns: new[] { "IdHogar", "Correo" },
                unique: true);
        }
    }
}
