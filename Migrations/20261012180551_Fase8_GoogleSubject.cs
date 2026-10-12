using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class Fase8_GoogleSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleSubject",
                table: "Usuarios",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_GoogleSubject",
                table: "Usuarios",
                column: "GoogleSubject",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_GoogleSubject",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "GoogleSubject",
                table: "Usuarios");
        }
    }
}
