using HolidayApp.Domain.Abstractions;
using HolidayApp.Infrastructure;
using HolidayApp.Web.Components;
using HolidayApp.Web.Endpoints;
using HolidayApp.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

// Blazor UI (runs in the browser on WebAssembly)
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Current user: the signed-in account (accounts with email and password, see README)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddAccounts();

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

// Without a session only the sign-in and register pages open
app.UseRateLimiter();
app.UseAccountGate();
app.MapAccountPages();

app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

// REST API at /api/... (signed-in accounts only)
var api = app.MapGroup("/api").RequireAuthorization();
api.MapEventEndpoints();
api.MapPlanEndpoints();
api.MapCalendarEndpoints();
api.MapSettingsEndpoints();
api.MapVisitedPlaceEndpoints();
api.MapAccountEndpoints();
api.MapSharingEndpoints();
api.MapFeedEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(HolidayApp.Client._Imports).Assembly);

app.Run();
