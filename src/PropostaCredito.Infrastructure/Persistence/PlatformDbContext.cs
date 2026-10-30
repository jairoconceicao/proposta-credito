using Microsoft.EntityFrameworkCore;
using PropostaCredito.Infrastructure.Modules.Empresas;
using PropostaCredito.Infrastructure.Modules.Propostas;
using PropostaCredito.Infrastructure.Modules.Shared;
using PropostaCredito.Infrastructure.Modules.Credito;

namespace PropostaCredito.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public const string Owner = "PC_OWNER";
    public DbSet<PlatformMetadata> Metadata => Set<PlatformMetadata>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMembership> Memberships => Set<CompanyMembership>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<ProposalHistory> ProposalHistory => Set<ProposalHistory>();
    public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditAnalysis> Analyses => Set<CreditAnalysis>();
    public DbSet<BureauQuery> BureauQueries => Set<BureauQuery>();
    public DbSet<CreditDecision> Decisions => Set<CreditDecision>();
    public DbSet<InboxEntry> Inbox => Set<InboxEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Owner);
        IntakeModel.Configure(modelBuilder);
        CreditModel.Configure(modelBuilder);
        modelBuilder.Entity<PlatformMetadata>(entity =>
        {
            entity.ToTable("PLATFORM_METADATA");
            entity.HasKey(row => row.Key);
            entity.Property(row => row.Key).HasColumnName("KEY").HasMaxLength(100);
            entity.Property(row => row.Value).HasColumnName("VALUE").HasMaxLength(200).IsRequired();
            entity.HasData(new PlatformMetadata { Key = "schema-version", Value = "1" });
        });
    }
}

public sealed class PlatformMetadata
{
    public required string Key { get; init; }
    public required string Value { get; set; }
}
