using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class Fase7_PasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResetToken",
                table: "Usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetTokenExpiry",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResetToken",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ResetTokenExpiry",
                table: "Usuarios");
        }
    }
}
