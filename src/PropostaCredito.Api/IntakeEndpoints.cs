using System.Diagnostics;
using PropostaCredito.Infrastructure.Modules.Empresas;
using PropostaCredito.Infrastructure.Modules.Propostas;
using PropostaCredito.Infrastructure.Modules.Shared;

namespace PropostaCredito.Api;

public static class IntakeEndpoints
{
    public static void MapIntake(this WebApplication app)
    {
        app.MapPost("/empresas", async (RegisterCompany request, HttpContext context, CompanyService service, CancellationToken ct) =>
        {
            var result = await service.RegisterAsync(request, Subject(context), Key(context), ct);
            return Results.Created($"/empresas/{result.EmpresaId}", result);
        }).RequireAuthorization("RegisterCompany");

        app.MapGet("/empresas/{id}", async (string id, HttpContext context, CompanyService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, Subject(context), context.User.IsInRole("Administrador"), ct)))
            .RequireAuthorization("ReadCompany");

        app.MapPost("/propostas", async (SubmitProposal request, HttpContext context, ProposalService service, CancellationToken ct) =>
        {
            var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
            var result = await service.SubmitAsync(request, Subject(context), Key(context), correlationId, ct);
            return Results.Accepted($"/propostas/{result.PropostaId}", result);
        }).RequireAuthorization("Company");

        app.MapGet("/propostas/{id}", async (string id, HttpContext context, ProposalService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, Subject(context), ct))).RequireAuthorization("Company");
    }

    private static string Subject(HttpContext context) => context.User.FindFirst("sub")!.Value;
    private static string Key(HttpContext context)
    {
        var values = context.Request.Headers["Idempotency-Key"];
        return IntakeValidation.IdempotencyKey(values.Count == 1 ? values[0] : null);
    }
}
