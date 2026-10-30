using PropostaCredito.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddFoundation();
var app = builder.Build();
app.MapFoundationHealth();
await app.RunAsync();

public partial class Program;
