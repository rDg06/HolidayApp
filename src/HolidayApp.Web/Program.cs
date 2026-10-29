using HolidayApp.Domain.Abstractions;
using HolidayApp.Infrastructure;
using HolidayApp.Web.Components;
using HolidayApp.Web.Endpoints;
using HolidayApp.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

// Interface Blazor (corre no navegador em WebAssembly)
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Utilizador atual: "local" em desenvolvimento; publicado, vem do login do App Service (ver README)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddAppServiceAuthentication(builder.Configuration);

// Base de dados + serviços da aplicação
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

await app.Services.InitialiseDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi(); // documento OpenAPI em /openapi/v1.json
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

// API REST em /api/... (só para utilizadores autorizados, fora de desenvolvimento)
var api = app.MapGroup("/api").RequireAllowedUser(app.Environment, app.Configuration);
api.MapEventEndpoints();
api.MapPlanEndpoints();
api.MapCalendarEndpoints();
api.MapSettingsEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(HolidayApp.Client._Imports).Assembly);

app.Run();

// Para os testes de integração (WebApplicationFactory<Program>)
public partial class Program;
