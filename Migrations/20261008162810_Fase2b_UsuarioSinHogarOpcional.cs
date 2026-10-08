using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class Fase2b_UsuarioSinHogarOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Rol",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "IdHogar",
                table: "Usuarios",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "IdUsuario",
                table: "MetasAhorro",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetasAhorro_IdUsuario",
                table: "MetasAhorro",
                column: "IdUsuario");

            migrationBuilder.AddForeignKey(
                name: "FK_MetasAhorro_Usuarios_IdUsuario",
                table: "MetasAhorro",
                column: "IdUsuario",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetasAhorro_Usuarios_IdUsuario",
                table: "MetasAhorro");

            migrationBuilder.DropIndex(
                name: "IX_MetasAhorro_IdUsuario",
                table: "MetasAhorro");

            migrationBuilder.DropColumn(
                name: "IdUsuario",
                table: "MetasAhorro");

            migrationBuilder.AlterColumn<string>(
                name: "Rol",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "IdHogar",
                table: "Usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
