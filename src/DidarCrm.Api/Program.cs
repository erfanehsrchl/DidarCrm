using DidarCrm.Api;
using DidarCrm.Api.Common;
using DidarCrm.Api.Consumers.Notifications;
using DidarCrm.Api.Consumers.Webhooks;
using DidarCrm.Api.Validation;
using DidarCrm.Application;
using DidarCrm.Infrastructure;
using DidarCrm.Infrastructure.Messaging;
using DidarCrm.Infrastructure.Webhooks;
using MassTransit;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Connection string 'Database' was not found.");
var rabbitMqOptions = builder.Configuration
    .GetSection(RabbitMqOptions.SectionName)
    .Get<RabbitMqOptions>()
    ?? throw new InvalidOperationException(
        $"Configuration section '{RabbitMqOptions.SectionName}' was not found.");
var outboxDeliveryOptions = builder.Configuration
    .GetSection(OutboxDeliveryOptions.SectionName)
    .Get<OutboxDeliveryOptions>()
    ?? throw new InvalidOperationException(
        $"Configuration section '{OutboxDeliveryOptions.SectionName}' was not found.");
var consumerProcessingOptions = builder.Configuration
    .GetSection(ConsumerProcessingOptions.SectionName)
    .Get<ConsumerProcessingOptions>()
    ?? throw new InvalidOperationException(
        $"Configuration section '{ConsumerProcessingOptions.SectionName}' was not found.");
var webhookOptions = builder.Configuration
    .GetSection(WebhookOptions.SectionName)
    .Get<WebhookOptions>()
    ?? throw new InvalidOperationException(
        $"Configuration section '{WebhookOptions.SectionName}' was not found.");

builder.Services.AddInfrastructure(
    connectionString,
    rabbitMqOptions,
    outboxDeliveryOptions,
    consumerProcessingOptions,
    webhookOptions,
    consumers =>
    {
        consumers.AddConsumer<
            ActivityCreatedNotificationConsumer,
            ActivityCreatedNotificationConsumerDefinition>();
        consumers.AddConsumer<
            ActivityCreatedWebhookConsumer,
            ActivityCreatedWebhookConsumerDefinition>();
    });
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
