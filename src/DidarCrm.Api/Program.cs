var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "didar-crm", status = "bootstrap" }));

app.Run();

public partial class Program;
