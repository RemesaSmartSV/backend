using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RemesaSmartSV.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hogares",
                columns: table => new
                {
                    IdHogar = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreFamiliar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hogares", x => x.IdHogar);
                });

            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    IdCategoria = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdHogar = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Icono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.IdCategoria);
                    table.ForeignKey(
                        name: "FK_Categorias_Hogares_IdHogar",
                        column: x => x.IdHogar,
                        principalTable: "Hogares",
                        principalColumn: "IdHogar",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetasAhorro",
                columns: table => new
                {
                    IdMeta = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdHogar = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MontoObjetivo = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    MontoActual = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    FechaLimite = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasAhorro", x => x.IdMeta);
                    table.ForeignKey(
                        name: "FK_MetasAhorro_Hogares_IdHogar",
                        column: x => x.IdHogar,
                        principalTable: "Hogares",
                        principalColumn: "IdHogar",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    IdUsuario = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdHogar = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Correo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContrasenaHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.IdUsuario);
                    table.ForeignKey(
                        name: "FK_Usuarios_Hogares_IdHogar",
                        column: x => x.IdHogar,
                        principalTable: "Hogares",
                        principalColumn: "IdHogar",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Presupuestos",
                columns: table => new
                {
                    IdPresupuesto = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdHogar = table.Column<int>(type: "integer", nullable: false),
                    IdCategoria = table.Column<int>(type: "integer", nullable: false),
                    MontoLimite = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    MesAnio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presupuestos", x => x.IdPresupuesto);
                    table.ForeignKey(
                        name: "FK_Presupuestos_Categorias_IdCategoria",
                        column: x => x.IdCategoria,
                        principalTable: "Categorias",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Presupuestos_Hogares_IdHogar",
                        column: x => x.IdHogar,
                        principalTable: "Hogares",
                        principalColumn: "IdHogar",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TipsFinancieros",
                columns: table => new
                {
                    IdTip = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdCategoria = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Contenido = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipsFinancieros", x => x.IdTip);
                    table.ForeignKey(
                        name: "FK_TipsFinancieros_Categorias_IdCategoria",
                        column: x => x.IdCategoria,
                        principalTable: "Categorias",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Aportes",
                columns: table => new
                {
                    IdAporte = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdMeta = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aportes", x => x.IdAporte);
                    table.ForeignKey(
                        name: "FK_Aportes_MetasAhorro_IdMeta",
                        column: x => x.IdMeta,
                        principalTable: "MetasAhorro",
                        principalColumn: "IdMeta",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Movimientos",
                columns: table => new
                {
                    IdMovimiento = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdHogar = table.Column<int>(type: "integer", nullable: false),
                    IdUsuario = table.Column<int>(type: "integer", nullable: false),
                    IdCategoria = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OrigenEmisora = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movimientos", x => x.IdMovimiento);
                    table.ForeignKey(
                        name: "FK_Movimientos_Categorias_IdCategoria",
                        column: x => x.IdCategoria,
                        principalTable: "Categorias",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Movimientos_Hogares_IdHogar",
                        column: x => x.IdHogar,
                        principalTable: "Hogares",
                        principalColumn: "IdHogar",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Movimientos_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aportes_IdMeta",
                table: "Aportes",
                column: "IdMeta");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_IdHogar",
                table: "Categorias",
                column: "IdHogar");

            migrationBuilder.CreateIndex(
                name: "IX_MetasAhorro_IdHogar",
                table: "MetasAhorro",
                column: "IdHogar");

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_IdCategoria",
                table: "Movimientos",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_IdHogar",
                table: "Movimientos",
                column: "IdHogar");

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_IdUsuario",
                table: "Movimientos",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_IdCategoria",
                table: "Presupuestos",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_IdHogar",
                table: "Presupuestos",
                column: "IdHogar");

            migrationBuilder.CreateIndex(
                name: "IX_TipsFinancieros_IdCategoria",
                table: "TipsFinancieros",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_IdHogar",
                table: "Usuarios",
                column: "IdHogar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aportes");

            migrationBuilder.DropTable(
                name: "Movimientos");

            migrationBuilder.DropTable(
                name: "Presupuestos");

            migrationBuilder.DropTable(
                name: "TipsFinancieros");

            migrationBuilder.DropTable(
                name: "MetasAhorro");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Categorias");

            migrationBuilder.DropTable(
                name: "Hogares");
        }
    }
}
