namespace DidarCrm.Application.Abstractions.Persistence;

public sealed class DuplicateWebhookDeliveryException(Exception innerException)
    : Exception("A webhook delivery already exists for this activity.", innerException);
