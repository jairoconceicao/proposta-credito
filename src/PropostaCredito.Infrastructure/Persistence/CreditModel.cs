using Microsoft.EntityFrameworkCore;
using PropostaCredito.Infrastructure.Modules.Credito;
using PropostaCredito.Infrastructure.Modules.Empresas;
using PropostaCredito.Infrastructure.Modules.Propostas;

namespace PropostaCredito.Infrastructure.Persistence;

internal static class CreditModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<CreditAccount>(entity =>
        {
            entity.ToTable("CREDITO_LIMITE"); entity.HasKey(row => row.CompanyId);
            entity.Property(row => row.CompanyId).HasMaxLength(32);
            entity.Property(row => row.TotalLimit).HasPrecision(14, 2);
            entity.Property(row => row.Reserved).HasPrecision(14, 2);
            entity.HasOne<Company>().WithOne().HasForeignKey<CreditAccount>(row => row.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CreditAnalysis>(entity =>
        {
            entity.ToTable("CREDITO_ANALISE"); entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasMaxLength(32);
            entity.Property(row => row.ProposalId).HasMaxLength(32); entity.HasIndex(row => row.ProposalId).IsUnique();
            entity.Property(row => row.RulesVersion).HasMaxLength(32);
            entity.Property(row => row.ProposalSnapshot).HasColumnType("CLOB");
            entity.Property(row => row.CurrentQueryId).HasMaxLength(32);
            entity.Property(row => row.Outcome).HasMaxLength(40);
            entity.Property(row => row.ReasonsJson).HasColumnType("CLOB");
            entity.HasOne<Proposal>().WithOne().HasForeignKey<CreditAnalysis>(row => row.ProposalId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<BureauQuery>(entity =>
        {
            entity.ToTable("BUREAU_CONSULTA"); entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasMaxLength(32);
            entity.Property(row => row.AnalysisId).HasMaxLength(32);
            entity.Property(row => row.ProposalId).HasMaxLength(32);
            entity.Property(row => row.State).HasMaxLength(40);
            entity.Property(row => row.Provider).HasMaxLength(40);
            entity.Property(row => row.CorrelationId).HasMaxLength(64).IsUnicode(false);
            entity.Property(row => row.CausationId).HasMaxLength(64).IsUnicode(false);
            entity.Property(row => row.TraceParent).HasMaxLength(128).IsUnicode(false);
            entity.Property(row => row.ExistingDebt).HasPrecision(14, 2);
            entity.Property(row => row.TotalLimit).HasPrecision(14, 2);
            entity.HasIndex(row => new { row.State, row.CreatedAt });
            entity.HasOne<CreditAnalysis>().WithMany().HasForeignKey(row => row.AnalysisId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CreditDecision>(entity =>
        {
            entity.ToTable("CREDITO_DECISAO"); entity.HasKey(row => row.AnalysisId);
            entity.Property(row => row.AnalysisId).HasMaxLength(32);
            entity.Property(row => row.ProposalId).HasMaxLength(32);
            entity.Property(row => row.QueryId).HasMaxLength(32);
            entity.Property(row => row.RulesVersion).HasMaxLength(32);
            entity.Property(row => row.Outcome).HasMaxLength(40);
            entity.Property(row => row.ReasonsJson).HasColumnType("CLOB");
            entity.Property(row => row.InputsJson).HasColumnType("CLOB");
            entity.Property(row => row.Author).HasMaxLength(100);
            entity.HasOne<CreditAnalysis>().WithOne().HasForeignKey<CreditDecision>(row => row.AnalysisId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<InboxEntry>(entity =>
        {
            entity.ToTable("INBOX"); entity.HasKey(row => new { row.Consumer, row.MessageId });
            entity.Property(row => row.Consumer).HasMaxLength(80).IsUnicode(false);
            entity.Property(row => row.MessageId).HasMaxLength(32);
            entity.Property(row => row.ProposalId).HasMaxLength(32);
            entity.Property(row => row.LogicalHash).HasMaxLength(64).IsUnicode(false);
            entity.Property(row => row.Result).HasMaxLength(80).IsUnicode(false);
        });
    }
}
