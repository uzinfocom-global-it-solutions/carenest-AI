using System.IdentityModel.Tokens.Jwt;
using Backend.Infrastructure.Data;
using Backend.Web;
using Backend.Web.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Local secrets (LLM tokens, etc.) live in `.env`. Loaded BEFORE the host
// builder so the standard environment-variable configuration source picks them
// up, where they override matching keys from `appsettings.json`.
// Use double-underscore for nested keys (e.g. `LLM__Token`).
// Searched in order: backend/.env, then ./.env (handy in containers).
DotEnvLoader.Load(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();

var app = builder.Build();

await app.InitialiseDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    // Production: TLS is terminated at the reverse proxy / load balancer
    app.UseHsts();
}
app.UseCors(WebDefaults.CorsPolicy);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseFileServer();

// OpenAPI JSON + Swagger UI.
// JSON spec:        /openapi/v1.json
// Interactive docs: /docs (also reachable via /swagger redirect)
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Backend API v1");
    options.DocumentTitle = "Backend API";
    options.RoutePrefix = "docs";
    options.EnablePersistAuthorization();
});
app.MapGet("/swagger", () => Results.Redirect("/docs"));

app.UseExceptionHandler(options => { });

app.Map("/", () => Results.Redirect("/docs"));

app.MapDefaultEndpoints();
app.MapEndpoints(typeof(Program).Assembly);

app.Run();
