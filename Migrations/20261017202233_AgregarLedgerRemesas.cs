using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class AgregarLedgerRemesas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Remesas",
                columns: table => new
                {
                    IdRemesa = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdHogar = table.Column<int>(type: "integer", nullable: false),
                    IdUsuario = table.Column<int>(type: "integer", nullable: true),
                    Direccion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MontoEnviado = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    MontoRecibido = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Comision = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Folio = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notas = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IdRemesaVinculada = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Remesas", x => x.IdRemesa);
                    table.ForeignKey(
                        name: "FK_Remesas_Hogares_IdHogar",
                        column: x => x.IdHogar,
                        principalTable: "Hogares",
                        principalColumn: "IdHogar",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Remesas_Remesas_IdRemesaVinculada",
                        column: x => x.IdRemesaVinculada,
                        principalTable: "Remesas",
                        principalColumn: "IdRemesa",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Remesas_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Remesas_Fecha",
                table: "Remesas",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Remesas_IdHogar",
                table: "Remesas",
                column: "IdHogar");

            migrationBuilder.CreateIndex(
                name: "IX_Remesas_IdRemesaVinculada",
                table: "Remesas",
                column: "IdRemesaVinculada");

            migrationBuilder.CreateIndex(
                name: "IX_Remesas_IdUsuario",
                table: "Remesas",
                column: "IdUsuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Remesas");
        }
    }
}
