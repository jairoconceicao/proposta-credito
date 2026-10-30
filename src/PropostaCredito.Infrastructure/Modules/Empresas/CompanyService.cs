using Microsoft.EntityFrameworkCore;
using PropostaCredito.Infrastructure.Modules.Shared;
using PropostaCredito.Infrastructure.Persistence;
using PropostaCredito.Infrastructure.Modules.Credito;

namespace PropostaCredito.Infrastructure.Modules.Empresas;

public sealed class Company
{
    public required string Id { get; init; }
    public required string Cnpj { get; init; }
    public required string Name { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class CompanyMembership
{
    public required string Subject { get; init; }
    public required string CompanyId { get; init; }
}

public sealed record RegisterCompany(string? Cnpj, string? RazaoSocial, string? ResponsavelId);
public sealed record CompanyResponse(string EmpresaId, string Cnpj, string RazaoSocial, DateTime CriadaEm);

public sealed class CompanyService(IdempotentCommandExecutor commands, IDbContextFactory<PlatformDbContext> factory)
{
    public async Task<CompanyResponse> RegisterAsync(RegisterCompany request, string administrator,
        string key, CancellationToken cancellationToken)
    {
        var cnpj = IntakeValidation.Cnpj(request.Cnpj);
        var name = IntakeValidation.Text(request.RazaoSocial, "razao-social", 200);
        var subject = IntakeValidation.Text(request.ResponsavelId, "responsavel-id", 128);
        var hash = IntakeValidation.Hash("company-v1", cnpj, name, subject);
        return await commands.ExecuteAsync("admin:" + administrator, "POST /empresas", key, hash, async database =>
        {
            var company = new Company { Id = Guid.NewGuid().ToString("N"), Cnpj = cnpj, Name = name, CreatedAt = DateTime.UtcNow };
            database.Companies.Add(company);
            database.CreditAccounts.Add(new CreditAccount { CompanyId = company.Id });
            database.Memberships.Add(new CompanyMembership { Subject = subject, CompanyId = company.Id });
            try
            {
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IdempotentCommandExecutor.IsUniqueViolation(exception))
            {
                throw new IntakeException(409, "company-conflict", "CNPJ ou responsavel ja cadastrado.");
            }
            return Response(company);
        }, cancellationToken);
    }

    public async Task<CompanyResponse> GetAsync(string id, string subject, bool administrator, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        if (!administrator && !await database.Memberships.AnyAsync(row => row.Subject == subject && row.CompanyId == id, cancellationToken))
            throw new IntakeException(404, "not-found", "Empresa nao encontrada.");
        var company = await database.Companies.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new IntakeException(404, "not-found", "Empresa nao encontrada.");
        return Response(company);
    }

    private static CompanyResponse Response(Company company) =>
        new(company.Id, company.Cnpj, company.Name, DateTime.SpecifyKind(company.CreatedAt, DateTimeKind.Utc));
}
