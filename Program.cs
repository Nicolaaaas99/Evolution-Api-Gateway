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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();