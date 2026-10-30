using PropostaCredito.Infrastructure;
using PropostaCredito.Worker;
using PropostaCredito.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.AddFoundation();
builder.AddCreditWorker();
builder.Services.AddHostedService<WorkerLifecycle>();
var app = builder.Build();
app.MapFoundationHealth();
await app.RunAsync();
