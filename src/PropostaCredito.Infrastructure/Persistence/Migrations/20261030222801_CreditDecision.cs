using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropostaCredito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CREDITO_ANALISE",
                schema: "PC_OWNER",
                columns: table => new
                {
                    Id = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ProposalId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    RulesVersion = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ProposalSnapshot = table.Column<string>(type: "CLOB", nullable: false),
                    CurrentQueryId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DeadlineAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    Outcome = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: true),
                    ReasonsJson = table.Column<string>(type: "CLOB", nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CREDITO_ANALISE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CREDITO_ANALISE_PROPOSTAS_ProposalId",
                        column: x => x.ProposalId,
                        principalSchema: "PC_OWNER",
                        principalTable: "PROPOSTAS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CREDITO_LIMITE",
                schema: "PC_OWNER",
                columns: table => new
                {
                    CompanyId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    TotalLimit = table.Column<decimal>(type: "DECIMAL(14,2)", precision: 14, scale: 2, nullable: false),
                    Reserved = table.Column<decimal>(type: "DECIMAL(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CREDITO_LIMITE", x => x.CompanyId);
                    table.ForeignKey(
                        name: "FK_CREDITO_LIMITE_EMPRESAS_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "PC_OWNER",
                        principalTable: "EMPRESAS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INBOX",
                schema: "PC_OWNER",
                columns: table => new
                {
                    Consumer = table.Column<string>(type: "VARCHAR2(80)", unicode: false, maxLength: 80, nullable: false),
                    MessageId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ProposalId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    LogicalHash = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: false),
                    Result = table.Column<string>(type: "VARCHAR2(80)", unicode: false, maxLength: 80, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INBOX", x => new { x.Consumer, x.MessageId });
                });

            migrationBuilder.CreateTable(
                name: "BUREAU_CONSULTA",
                schema: "PC_OWNER",
                columns: table => new
                {
                    Id = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    AnalysisId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ProposalId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    Provider = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    CorrelationId = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: false),
                    CausationId = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: false),
                    TraceParent = table.Column<string>(type: "VARCHAR2(128)", unicode: false, maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ObtainedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ExistingDebt = table.Column<decimal>(type: "DECIMAL(14,2)", precision: 14, scale: 2, nullable: true),
                    TotalLimit = table.Column<decimal>(type: "DECIMAL(14,2)", precision: 14, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BUREAU_CONSULTA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BUREAU_CONSULTA_CREDITO_ANALISE_AnalysisId",
                        column: x => x.AnalysisId,
                        principalSchema: "PC_OWNER",
                        principalTable: "CREDITO_ANALISE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CREDITO_DECISAO",
                schema: "PC_OWNER",
                columns: table => new
                {
                    AnalysisId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ProposalId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    QueryId = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    RulesVersion = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    ReasonsJson = table.Column<string>(type: "CLOB", nullable: false),
                    InputsJson = table.Column<string>(type: "CLOB", nullable: false),
                    Author = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CREDITO_DECISAO", x => x.AnalysisId);
                    table.ForeignKey(
                        name: "FK_CREDITO_DECISAO_CREDITO_ANALISE_AnalysisId",
                        column: x => x.AnalysisId,
                        principalSchema: "PC_OWNER",
                        principalTable: "CREDITO_ANALISE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BUREAU_CONSULTA_AnalysisId",
                schema: "PC_OWNER",
                table: "BUREAU_CONSULTA",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_BUREAU_CONSULTA_State_CreatedAt",
                schema: "PC_OWNER",
                table: "BUREAU_CONSULTA",
                columns: new[] { "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CREDITO_ANALISE_ProposalId",
                schema: "PC_OWNER",
                table: "CREDITO_ANALISE",
                column: "ProposalId",
                unique: true);

            migrationBuilder.Sql("INSERT INTO PC_OWNER.CREDITO_LIMITE (\"CompanyId\", \"TotalLimit\", \"Reserved\") SELECT \"Id\", 50000, 0 FROM PC_OWNER.EMPRESAS");
            migrationBuilder.Sql("ALTER TABLE PC_OWNER.CREDITO_LIMITE ADD CONSTRAINT CK_LIMITE_RESERVADO CHECK (\"Reserved\" >= 0 AND \"Reserved\" <= \"TotalLimit\")");
            foreach (var table in new[] { "CREDITO_LIMITE", "CREDITO_ANALISE", "BUREAU_CONSULTA", "CREDITO_DECISAO", "INBOX" })
                migrationBuilder.Sql($"GRANT SELECT, INSERT ON PC_OWNER.{table} TO PC_APP");
            migrationBuilder.Sql("GRANT UPDATE (\"Reserved\") ON PC_OWNER.CREDITO_LIMITE TO PC_APP");
            migrationBuilder.Sql("GRANT UPDATE (\"CurrentQueryId\", \"Outcome\", \"ReasonsJson\", \"EvaluatedAt\") ON PC_OWNER.CREDITO_ANALISE TO PC_APP");
            migrationBuilder.Sql("GRANT UPDATE (\"State\", \"ObtainedAt\", \"ValidUntil\", \"ExistingDebt\", \"TotalLimit\") ON PC_OWNER.BUREAU_CONSULTA TO PC_APP");
            migrationBuilder.Sql("GRANT UPDATE (\"State\", \"Version\") ON PC_OWNER.PROPOSTAS TO PC_APP");
            migrationBuilder.Sql("GRANT UPDATE (\"AvailableAt\", \"LeaseUntil\", \"LeaseToken\", \"PublishedAt\", \"Error\") ON PC_OWNER.OUTBOX TO PC_APP");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BUREAU_CONSULTA",
                schema: "PC_OWNER");

            migrationBuilder.DropTable(
                name: "CREDITO_DECISAO",
                schema: "PC_OWNER");

            migrationBuilder.DropTable(
                name: "CREDITO_LIMITE",
                schema: "PC_OWNER");

            migrationBuilder.DropTable(
                name: "INBOX",
                schema: "PC_OWNER");

            migrationBuilder.DropTable(
                name: "CREDITO_ANALISE",
                schema: "PC_OWNER");
        }
    }
}
