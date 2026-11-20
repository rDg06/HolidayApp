using HolidayApp.Domain.Abstractions;
using HolidayApp.Infrastructure;
using HolidayApp.Web.Components;
using HolidayApp.Web.Endpoints;
using HolidayApp.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

// Blazor UI (runs in the browser on WebAssembly)
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Current user: the signed-in account, the App Service login, or "local" (see README)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddAppServiceAuthentication(builder.Configuration);

// Database + application services
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

await app.Services.InitialiseDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi(); // OpenAPI document at /openapi/v1.json
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();

// Accounts (with an invite code): without a session only the sign-in and register pages open
if (Accounts.IsEnabled(app.Configuration))
{
    app.UseRateLimiter();
    app.UseAccountGate();
    app.MapAccountPages();
}

app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

// REST API at /api/... (only for allowed users, outside development)
var api = app.MapGroup("/api").RequireAllowedUser(app.Environment, app.Configuration);
api.MapEventEndpoints();
api.MapPlanEndpoints();
api.MapCalendarEndpoints();
api.MapSettingsEndpoints();
api.MapVisitedPlaceEndpoints();
api.MapAccountEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(HolidayApp.Client._Imports).Assembly);

app.Run();
