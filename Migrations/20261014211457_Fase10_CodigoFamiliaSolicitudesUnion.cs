using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class Fase10_CodigoFamiliaSolicitudesUnion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoPublico",
                table: "Hogares",
                type: "character varying(23)",
                maxLength: 23,
                nullable: true);

            // Backfill aditivo: UUID v4 del servidor como fuente CSPRNG; conservar IDs y relaciones.
            migrationBuilder.Sql("""
                DO $$
                DECLARE hogar record;
                        codigo text;
                BEGIN
                    FOR hogar IN SELECT "IdHogar" FROM "Hogares" WHERE "CodigoPublico" IS NULL LOOP
                        LOOP
                            codigo := 'RS-FAM-' || upper(substr(replace(gen_random_uuid()::text, '-', ''), 1, 16));
                            EXIT WHEN NOT EXISTS (SELECT 1 FROM "Hogares" WHERE "CodigoPublico" = codigo);
                        END LOOP;
                        UPDATE "Hogares" SET "CodigoPublico" = codigo WHERE "IdHogar" = hogar."IdHogar";
                    END LOOP;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CodigoPublico",
                table: "Hogares",
                type: "character varying(23)",
                maxLength: 23,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(23)",
                oldMaxLength: 23,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "SolicitudesUnion",
                columns: table => new
                {
                    IdSolicitud = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdHogar = table.Column<int>(type: "integer", nullable: false),
                    IdUsuario = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaRespuesta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdAdminRespuesta = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesUnion", x => x.IdSolicitud);
                    table.ForeignKey(
                        name: "FK_SolicitudesUnion_Hogares_IdHogar",
                        column: x => x.IdHogar,
                        principalTable: "Hogares",
                        principalColumn: "IdHogar",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SolicitudesUnion_Usuarios_IdAdminRespuesta",
                        column: x => x.IdAdminRespuesta,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SolicitudesUnion_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hogares_CodigoPublico",
                table: "Hogares",
                column: "CodigoPublico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesUnion_IdAdminRespuesta",
                table: "SolicitudesUnion",
                column: "IdAdminRespuesta");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesUnion_IdHogar_IdUsuario",
                table: "SolicitudesUnion",
                columns: new[] { "IdHogar", "IdUsuario" },
                unique: true,
                filter: "\"Estado\" = 'Pendiente'");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesUnion_IdUsuario",
                table: "SolicitudesUnion",
                column: "IdUsuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitudesUnion");

            migrationBuilder.DropIndex(
                name: "IX_Hogares_CodigoPublico",
                table: "Hogares");

            migrationBuilder.DropColumn(
                name: "CodigoPublico",
                table: "Hogares");
        }
    }
}
