using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class Fase11_FotoPerfilArchivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "FotoArchivo",
                table: "Usuarios",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoContentType",
                table: "Usuarios",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoArchivo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FotoContentType",
                table: "Usuarios");
        }
    }
}
