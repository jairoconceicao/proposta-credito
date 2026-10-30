using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace PropostaCredito.Api.Security;

public static class ApiAuthentication
{
    public static readonly string[] Roles = ["Administrador", "Empresa", "Analista", "Operador"];

    public static void AddApiAuthentication(this WebApplicationBuilder builder)
    {
        var mode = builder.Configuration["Authentication:Mode"] ?? "External";
        if (mode == "Development")
        {
            if (!builder.Environment.IsDevelopment())
                throw new InvalidOperationException("Autenticacao de laboratorio exige ambiente Development.");
            builder.Services.AddOptions<DevelopmentIdentities>().Bind(builder.Configuration.GetSection("DevelopmentAuth"))
                .Validate(options => options.Identities.Length > 0 && options.Identities.All(identity =>
                    identity.Subject.Length is > 0 and <= 128 && !identity.Subject.Any(char.IsControl)
                    && Roles.Contains(identity.Role) && identity.Token.Length is >= 32 and <= 128)
                    && options.Identities.Select(identity => identity.Token).Distinct().Count() == options.Identities.Length,
                    "Configure identidades ficticias com tokens unicos de 32 a 128 caracteres.")
                .ValidateOnStart();
            builder.Services.AddAuthentication("Development")
                .AddScheme<AuthenticationSchemeOptions, DevelopmentTokenHandler>("Development", _ => { });
        }
        else if (mode == "External")
        {
            var authority = builder.Configuration["Authentication:Authority"];
            var audience = builder.Configuration["Authentication:Audience"];
            if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) || uri.Scheme != "https" || string.IsNullOrWhiteSpace(audience))
                throw new InvalidOperationException("Configure Authentication:Authority HTTPS e Authentication:Audience para JWT externo.");
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.Authority = authority; options.Audience = audience;
                options.MapInboundClaims = false; options.IncludeErrorDetails = false;
                options.TokenValidationParameters.RoleClaimType = "role";
                options.TokenValidationParameters.NameClaimType = "sub";
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var subjects = context.Principal?.FindAll("sub").ToArray() ?? [];
                        if (subjects.Length != 1 || subjects[0].Value.Length is 0 or > 128 || subjects[0].Value.Any(char.IsControl))
                            context.Fail("Identidade invalida.");
                        return Task.CompletedTask;
                    }
                };
            });
        }
        else throw new InvalidOperationException("Modo de autenticacao desconhecido.");

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("RegisterCompany", policy => policy.RequireAuthenticatedUser().RequireRole("Administrador"));
            options.AddPolicy("ReadCompany", policy => policy.RequireAuthenticatedUser().RequireRole("Administrador", "Empresa"));
            options.AddPolicy("Company", policy => policy.RequireAuthenticatedUser().RequireRole("Empresa"));
        });
    }
}

public sealed class DevelopmentIdentities
{
    public DevelopmentIdentity[] Identities { get; set; } = [];
}
public sealed class DevelopmentIdentity
{
    public string Subject { get; set; } = "";
    public string Role { get; set; } = "";
    public string Token { get; set; } = "";
}

public sealed class DevelopmentTokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IOptions<DevelopmentIdentities> identities)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization;
        if (authorization.Count == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var header = authorization.ToString();
        if (authorization.Count != 1 || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || header.Length > 135)
            return Task.FromResult(AuthenticateResult.Fail("Credencial invalida."));
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(header[7..]));
        var identity = identities.Value.Identities.FirstOrDefault(candidate =>
            CryptographicOperations.FixedTimeEquals(digest, SHA256.HashData(Encoding.UTF8.GetBytes(candidate.Token))));
        if (identity is null) return Task.FromResult(AuthenticateResult.Fail("Credencial invalida."));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", identity.Subject), new Claim(ClaimTypes.Role, identity.Role)], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
