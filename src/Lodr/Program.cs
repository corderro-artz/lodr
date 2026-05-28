using Lodr;
using Lodr.Interop;
using Lodr.Services;
using Lodr.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<OrientationService>();
builder.Services.AddScoped<FitCalculator>();
builder.Services.AddScoped<PlacementEngine>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<PersistenceInterop>();
builder.Services.AddScoped<VisualizationInterop>();

await builder.Build().RunAsync();
