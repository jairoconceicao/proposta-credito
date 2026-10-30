using Microsoft.EntityFrameworkCore;
using PropostaCredito.Infrastructure.Modules.Empresas;
using PropostaCredito.Infrastructure.Modules.Propostas;
using PropostaCredito.Infrastructure.Modules.Shared;

namespace PropostaCredito.Infrastructure.Persistence;

internal static class IntakeModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<Company>(entity =>
        {
            entity.ToTable("EMPRESAS"); entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasMaxLength(32);
            entity.Property(row => row.Cnpj).HasMaxLength(14).IsUnicode(false);
            entity.HasIndex(row => row.Cnpj).IsUnique();
            entity.Property(row => row.Name).HasMaxLength(200);
        });
        model.Entity<CompanyMembership>(entity =>
        {
            entity.ToTable("IDENTIDADE_EMPRESA"); entity.HasKey(row => row.Subject);
            entity.Property(row => row.Subject).HasMaxLength(128);
            entity.Property(row => row.CompanyId).HasMaxLength(32);
            entity.HasOne<Company>().WithMany().HasForeignKey(row => row.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Proposal>(entity =>
        {
            entity.ToTable("PROPOSTAS"); entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasMaxLength(32);
            entity.Property(row => row.CompanyId).HasMaxLength(32);
            entity.Property(row => row.Cnpj).HasMaxLength(14).IsUnicode(false);
            entity.Property(row => row.AnalysisId).HasMaxLength(32);
            entity.Property(row => row.MonthlyRevenue).HasPrecision(14, 2);
            entity.Property(row => row.Amount).HasPrecision(14, 2);
            entity.Property(row => row.Currency).HasMaxLength(3).IsUnicode(false);
            entity.Property(row => row.State).HasMaxLength(40).IsUnicode(false);
            entity.Property(row => row.Version).IsConcurrencyToken();
            entity.HasOne<Company>().WithMany().HasForeignKey(row => row.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ProposalHistory>(entity =>
        {
            entity.ToTable("PROPOSTA_HISTORICO"); entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasMaxLength(32);
            entity.Property(row => row.ProposalId).HasMaxLength(32);
            entity.Property(row => row.State).HasMaxLength(40).IsUnicode(false);
            entity.Property(row => row.Actor).HasMaxLength(128);
            entity.HasOne<Proposal>().WithMany().HasForeignKey(row => row.ProposalId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IDEMPOTENCIA"); entity.HasKey(row => new { row.Scope, row.Operation, row.Key });
            entity.Property(row => row.Scope).HasMaxLength(140);
            entity.Property(row => row.Operation).HasMaxLength(40).IsUnicode(false);
            entity.Property(row => row.Key).HasMaxLength(128).IsUnicode(false);
            entity.Property(row => row.BodyHash).HasMaxLength(64).IsUnicode(false);
            entity.Property(row => row.ResponseJson).HasColumnType("CLOB");
        });
        model.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OUTBOX"); entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasMaxLength(32);
            entity.Property(row => row.MessageId).HasMaxLength(32);
            entity.HasIndex(row => row.MessageId).IsUnique();
            entity.Property(row => row.ProposalId).HasMaxLength(32);
            entity.Property(row => row.Type).HasMaxLength(100).IsUnicode(false);
            entity.Property(row => row.AnalysisId).HasMaxLength(32);
            entity.Property(row => row.RoutingKey).HasMaxLength(100).IsUnicode(false);
            entity.Property(row => row.CorrelationId).HasMaxLength(64).IsUnicode(false);
            entity.Property(row => row.CausationId).HasMaxLength(64).IsUnicode(false);
            entity.Property(row => row.TraceParent).HasMaxLength(128).IsUnicode(false);
            entity.Property(row => row.Payload).HasColumnType("CLOB");
            entity.Property(row => row.ReplayId).HasMaxLength(32).IsUnicode(false);
            entity.Property(row => row.LeaseToken).HasMaxLength(32).IsUnicode(false);
            entity.Property(row => row.Error).HasMaxLength(1000);
            entity.HasIndex(row => new { row.PublishedAt, row.AvailableAt });
            entity.HasOne<Proposal>().WithMany().HasForeignKey(row => row.ProposalId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
