using Lodr;
using Lodr.Interop;
using Lodr.Services;
using Lodr.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Logging
var logService = new AppLogService();
builder.Services.AddSingleton<AppLogService>(logService);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(logService);

// Services
builder.Services.AddScoped<OrientationService>();
builder.Services.AddScoped<CapacityService>();
builder.Services.AddScoped<SlotService>();

// State
builder.Services.AddScoped<WorkspaceState>();

// Interop
builder.Services.AddScoped<PersistenceInterop>();
builder.Services.AddScoped<WorkspacePersistenceInterop>();
builder.Services.AddScoped<VisualizationInterop>();

await builder.Build().RunAsync();
