using DidarCrm.Api;
using DidarCrm.Api.Common;
using DidarCrm.Api.Validation;
using DidarCrm.Application;
using DidarCrm.Infrastructure;
using DidarCrm.Infrastructure.Messaging;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Connection string 'Database' was not found.");
var rabbitMqOptions = builder.Configuration
    .GetSection(RabbitMqOptions.SectionName)
    .Get<RabbitMqOptions>()
    ?? throw new InvalidOperationException(
        $"Configuration section '{RabbitMqOptions.SectionName}' was not found.");

builder.Services.AddInfrastructure(connectionString, rabbitMqOptions);
builder.Services.AddApplication(typeof(CreateActivityRequestValidator).Assembly);
builder.Services.AddPresentation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Didar CRM API",
            Version = "v1"
        });
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

public partial class Program;
