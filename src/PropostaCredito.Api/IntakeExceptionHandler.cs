using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PropostaCredito.Infrastructure.Modules.Shared;

namespace PropostaCredito.Api;

public sealed class IntakeExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            IntakeException intake => intake.Status,
            BadHttpRequestException malformed when malformed.StatusCode is >= 400 and < 500 => malformed.StatusCode,
            _ => 503
        };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = exception is IntakeException known ? known.Message : status < 500
                ? "Corpo da requisicao invalido." : "Operacao indisponivel. Tente novamente com a mesma chave.",
            Type = exception is IntakeException failure ? $"urn:proposta-credito:{failure.Code}" : status < 500
                ? "urn:proposta-credito:invalid-body" : "urn:proposta-credito:unavailable",
            Extensions = { ["traceId"] = context.TraceIdentifier }
        }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
