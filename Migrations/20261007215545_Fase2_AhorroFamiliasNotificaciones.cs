using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class Fase2_AhorroFamiliasNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "MetasAhorro",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "MetasAhorro",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql(
                "ALTER TABLE \"MetasAhorro\" ALTER COLUMN \"FechaCreacion\" DROP DEFAULT;");

            migrationBuilder.AddColumn<bool>(
                name: "EsSistema",
                table: "Categorias",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IdHogar",
                table: "Aportes",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Aportes\" a SET \"IdHogar\" = m.\"IdHogar\" " +
                "FROM \"MetasAhorro\" m WHERE a.\"IdMeta\" = m.\"IdMeta\" AND a.\"IdHogar\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "IdHogar",
                table: "Aportes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdUsuario",
                table: "Aportes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notas",
                table: "Aportes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvitacionesFamilia",
                columns: table => new
                {
                    IdInvitacion = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdHogar = table.Column<int>(type: "integer", nullable: false),
                    IdInvitador = table.Column<int>(type: "integer", nullable: false),
                    IdInvitado = table.Column<int>(type: "integer", nullable: true),
                    Correo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaRespuesta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitacionesFamilia", x => x.IdInvitacion);
                    table.ForeignKey(
                        name: "FK_InvitacionesFamilia_Hogares_IdHogar",
                        column: x => x.IdHogar,
                        principalTable: "Hogares",
                        principalColumn: "IdHogar",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvitacionesFamilia_Usuarios_IdInvitado",
                        column: x => x.IdInvitado,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InvitacionesFamilia_Usuarios_IdInvitador",
                        column: x => x.IdInvitador,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    IdNotificacion = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdUsuario = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Mensaje = table.Column<string>(type: "text", nullable: false),
                    Leida = table.Column<bool>(type: "boolean", nullable: false),
                    IdReferencia = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.IdNotificacion);
                    table.ForeignKey(
                        name: "FK_Notificaciones_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aportes_IdHogar",
                table: "Aportes",
                column: "IdHogar");

            migrationBuilder.CreateIndex(
                name: "IX_Aportes_IdUsuario",
                table: "Aportes",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesFamilia_IdHogar_Correo",
                table: "InvitacionesFamilia",
                columns: new[] { "IdHogar", "Correo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesFamilia_IdInvitado",
                table: "InvitacionesFamilia",
                column: "IdInvitado");

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesFamilia_IdInvitador",
                table: "InvitacionesFamilia",
                column: "IdInvitador");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_IdUsuario_Leida",
                table: "Notificaciones",
                columns: new[] { "IdUsuario", "Leida" });

            migrationBuilder.AddForeignKey(
                name: "FK_Aportes_Hogares_IdHogar",
                table: "Aportes",
                column: "IdHogar",
                principalTable: "Hogares",
                principalColumn: "IdHogar",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Aportes_Usuarios_IdUsuario",
                table: "Aportes",
                column: "IdUsuario",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Aportes_Hogares_IdHogar",
                table: "Aportes");

            migrationBuilder.DropForeignKey(
                name: "FK_Aportes_Usuarios_IdUsuario",
                table: "Aportes");

            migrationBuilder.DropTable(
                name: "InvitacionesFamilia");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropIndex(
                name: "IX_Aportes_IdHogar",
                table: "Aportes");

            migrationBuilder.DropIndex(
                name: "IX_Aportes_IdUsuario",
                table: "Aportes");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "MetasAhorro");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "MetasAhorro");

            migrationBuilder.DropColumn(
                name: "EsSistema",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "IdHogar",
                table: "Aportes");

            migrationBuilder.DropColumn(
                name: "IdUsuario",
                table: "Aportes");

            migrationBuilder.DropColumn(
                name: "Notas",
                table: "Aportes");
        }
    }
}
