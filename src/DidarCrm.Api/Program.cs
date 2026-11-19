using DidarCrm.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Connection string 'Database' was not found.");

builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "didar-crm", status = "bootstrap" }));

app.Run();

public partial class Program;
