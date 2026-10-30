using PropostaCredito.Infrastructure;
using PropostaCredito.Api;
using PropostaCredito.Api.Security;
using PropostaCredito.Infrastructure.Modules.Empresas;
using PropostaCredito.Infrastructure.Modules.Propostas;
using PropostaCredito.Infrastructure.Modules.Shared;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.AddFoundation();
builder.AddApiAuthentication();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddScoped<IdempotentCommandExecutor>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<ProposalService>();
builder.Services.AddSingleton<IProposalCommitHook, NoOpProposalCommitHook>();
builder.Services.AddSingleton<IIdempotencyReservationProbe, NoOpReservationProbe>();
builder.Services.AddExceptionHandler<IntakeExceptionHandler>();
builder.Services.AddProblemDetails();
// Default exception diagnostics may contain provider SQL/data; public handler emits sanitized problems.
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics", LogLevel.None);
var app = builder.Build();
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
app.UseAuthentication();
app.UseAuthorization();
app.MapFoundationHealth();
app.MapIntake();
await app.RunAsync();

public partial class Program;
