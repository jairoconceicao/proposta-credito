using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropostaCredito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EMPRESAS",
                schema: "PC_OWNER",
                columns: table => new
                {
                    Id = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    Cnpj = table.Column<string>(type: "VARCHAR2(14)", unicode: false, maxLength: 14, nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EMPRESAS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IDEMPOTENCIA",
                schema: "PC_OWNER",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "NVARCHAR2(140)", maxLength: 140, nullable: false),
                    Operation = table.Column<string>(type: "VARCHAR2(40)", unicode: false, maxLength: 40, nullable: false),
                    Key = table.Column<string>(type: "VARCHAR2(128)", unicode: false, maxLength: 128, nullable: false),
                    BodyHash = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "CLOB", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDEMPOTENCIA", x => new { x.Scope, x.Operation, x.Key });
                });

            migrationBuilder.CreateTable(
                name: "IDENTIDADE_EMPRESA",
                schema: "PC_OWNER",
                columns: table => new
                {
                    Subject = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    CompanyId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDENTIDADE_EMPRESA", x => x.Subject);
                    table.ForeignKey(
                        name: "FK_IDENTIDADE_EMPRESA_EMPRESAS_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "PC_OWNER",
                        principalTable: "EMPRESAS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PROPOSTAS",
                schema: "PC_OWNER",
                columns: table => new
                {
                    Id = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    CompanyId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    AnalysisId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    Cnpj = table.Column<string>(type: "VARCHAR2(14)", unicode: false, maxLength: 14, nullable: false),
                    MonthlyRevenue = table.Column<decimal>(type: "DECIMAL(14,2)", precision: 14, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "DECIMAL(14,2)", precision: 14, scale: 2, nullable: false),
                    Months = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Currency = table.Column<string>(type: "VARCHAR2(3)", unicode: false, maxLength: 3, nullable: false),
                    State = table.Column<string>(type: "VARCHAR2(40)", unicode: false, maxLength: 40, nullable: false),
                    Version = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROPOSTAS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PROPOSTAS_EMPRESAS_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "PC_OWNER",
                        principalTable: "EMPRESAS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OUTBOX",
                schema: "PC_OWNER",
                columns: table => new
                {
                    Id = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    MessageId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ProposalId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    AnalysisId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    RoutingKey = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    SchemaVersion = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CorrelationId = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: false),
                    CausationId = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: false),
                    TraceParent = table.Column<string>(type: "VARCHAR2(128)", unicode: false, maxLength: 128, nullable: true),
                    Payload = table.Column<string>(type: "CLOB", nullable: false),
                    Attempt = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ReplayId = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    AvailableAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LeaseToken = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    Error = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OUTBOX", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OUTBOX_PROPOSTAS_ProposalId",
                        column: x => x.ProposalId,
                        principalSchema: "PC_OWNER",
                        principalTable: "PROPOSTAS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PROPOSTA_HISTORICO",
                schema: "PC_OWNER",
                columns: table => new
                {
                    Id = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ProposalId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "VARCHAR2(40)", unicode: false, maxLength: 40, nullable: false),
                    Actor = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROPOSTA_HISTORICO", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PROPOSTA_HISTORICO_PROPOSTAS_ProposalId",
                        column: x => x.ProposalId,
                        principalSchema: "PC_OWNER",
                        principalTable: "PROPOSTAS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EMPRESAS_Cnpj",
                schema: "PC_OWNER",
                table: "EMPRESAS",
                column: "Cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IDENTIDADE_EMPRESA_CompanyId",
                schema: "PC_OWNER",
                table: "IDENTIDADE_EMPRESA",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_OUTBOX_MessageId",
                schema: "PC_OWNER",
                table: "OUTBOX",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OUTBOX_ProposalId",
                schema: "PC_OWNER",
                table: "OUTBOX",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_OUTBOX_PublishedAt_AvailableAt",
                schema: "PC_OWNER",
                table: "OUTBOX",
                columns: new[] { "PublishedAt", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PROPOSTA_HISTORICO_ProposalId",
                schema: "PC_OWNER",
                table: "PROPOSTA_HISTORICO",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_PROPOSTAS_CompanyId",
                schema: "PC_OWNER",
                table: "PROPOSTAS",
                column: "CompanyId");

            // Runtime can only read/append intake records. Only the response of a reserved key is updated.
            foreach (var table in new[] { "EMPRESAS", "IDENTIDADE_EMPRESA", "PROPOSTAS", "PROPOSTA_HISTORICO", "OUTBOX" })
                migrationBuilder.Sql($"GRANT SELECT, INSERT ON PC_OWNER.{table} TO PC_APP");
            migrationBuilder.Sql("GRANT SELECT, INSERT ON PC_OWNER.IDEMPOTENCIA TO PC_APP");
            migrationBuilder.Sql("GRANT UPDATE (\"ResponseJson\") ON PC_OWNER.IDEMPOTENCIA TO PC_APP");
            migrationBuilder.Sql("ALTER TABLE PC_OWNER.PROPOSTAS ADD CONSTRAINT CK_PROPOSTAS_VALORES CHECK (\"MonthlyRevenue\" > 0 AND \"Amount\" > 0 AND \"Months\" BETWEEN 1 AND 60 AND \"Currency\" = 'BRL')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IDEMPOTENCIA",
                schema: "PC_OWNER");

            migrationBuilder.DropTable(
                name: "IDENTIDADE_EMPRESA",
                schema: "PC_OWNER");

            migrationBuilder.DropTable(
                name: "OUTBOX",
                schema: "PC_OWNER");

            migrationBuilder.DropTable(
                name: "PROPOSTA_HISTORICO",
                schema: "PC_OWNER");

            migrationBuilder.DropTable(
                name: "PROPOSTAS",
                schema: "PC_OWNER");

            migrationBuilder.DropTable(
                name: "EMPRESAS",
                schema: "PC_OWNER");
        }
    }
}
