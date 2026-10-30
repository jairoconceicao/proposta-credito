using Microsoft.EntityFrameworkCore;
using PropostaCredito.Infrastructure.Persistence;

try
{
    await using var database = new DesignTimeDbContextFactory().CreateDbContext([]);
    await database.Database.MigrateAsync();
    Console.WriteLine("Migrations Oracle aplicadas; nenhuma credencial foi registrada.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Falha nas migrations ({exception.GetType().Name}). Confira conexao, permissoes e schema; consulte docs/DEVELOPMENT.md.");
    return 1;
}
