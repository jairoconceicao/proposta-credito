using PropostaCredito.Infrastructure;
using PropostaCredito.Worker;

var builder = WebApplication.CreateBuilder(args);
builder.AddFoundation();
builder.Services.AddHostedService<WorkerLifecycle>();
var app = builder.Build();
app.MapFoundationHealth();
await app.RunAsync();
