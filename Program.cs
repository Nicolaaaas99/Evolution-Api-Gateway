using EvolutionApiGateway.Configuration;
using EvolutionApiGateway.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Evolution settings from appsettings.json
builder.Services.Configure<EvolutionConfig>(
    builder.Configuration.GetSection("EvolutionConfig")
);

// Register your services
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<ViewDataService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseStaticFiles();

// --- SWAGGER CONFIGURATION ---
// We move this outside the IsDevelopment check for now to ensure IIS displays it.
// You can move it back once you confirm it works.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    // The "./v1/swagger.json" is the "magic" fix for IIS blank pages.
    // It tells the UI to look for the JSON file relative to the current URL.
    c.SwaggerEndpoint("./v1/swagger.json", "Evolution API Gateway v1");
});
// -----------------------------

// Important: If you want to access via http://localhost:8080 (no /swagger), 
// add: c.RoutePrefix = string.Empty; inside UseSwaggerUI.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();