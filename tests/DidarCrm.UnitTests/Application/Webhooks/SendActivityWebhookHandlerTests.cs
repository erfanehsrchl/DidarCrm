using DidarCrm.Application.Abstractions.Persistence;
using DidarCrm.Application.Abstractions.Time;
using DidarCrm.Application.Abstractions.Webhooks;
using DidarCrm.Application.Webhooks.SendActivityWebhook;
using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Webhooks;

namespace DidarCrm.UnitTests.Application.Webhooks;

public sealed class SendActivityWebhookHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 20, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_creates_pending_before_sending_and_marks_succeeded()
    {
        var repository = new WebhookDeliveryRepositoryFake();
        var sender = new WebhookSenderFake();
        var unitOfWork = new UnitOfWorkFake();
        var handler = new SendActivityWebhookHandler(
            sender,
            repository,
            unitOfWork,
            new ClockFake(Now));
        var command = CreateCommand();

        await handler.HandleAsync(command);

        var delivery = Assert.Single(repository.AddedDeliveries);
        Assert.NotEqual(Guid.Empty, delivery.Id.Value);
        Assert.Equal(command.ActivityId, delivery.ActivityId.Value);
        Assert.Equal(WebhookDeliveryStatus.Succeeded, delivery.Status);
        Assert.Equal(command.ActivityId, sender.IdempotencyKey);
        Assert.Equal(command.ActivityId, sender.Payload?.ActivityId);
        Assert.Equal(Now, delivery.LastAttemptAtUtc);
        Assert.Null(delivery.LastError);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal(2, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_skips_delivery_that_already_succeeded()
    {
        var delivery = WebhookDelivery.Create(
            ActivityId.From(Guid.NewGuid()),
            Now);
        delivery.MarkSucceeded(Now);
        var repository = new WebhookDeliveryRepositoryFake
        {
            ExistingDelivery = delivery
        };
        var sender = new WebhookSenderFake();
        var unitOfWork = new UnitOfWorkFake();
        var handler = new SendActivityWebhookHandler(
            sender,
            repository,
            unitOfWork,
            new ClockFake(Now));

        await handler.HandleAsync(CreateCommand(delivery.ActivityId.Value));

        Assert.Null(sender.Payload);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_stops_before_http_when_unique_activity_was_won_by_another_message()
    {
        var repository = new WebhookDeliveryRepositoryFake();
        var sender = new WebhookSenderFake();
        var unitOfWork = new UnitOfWorkFake
        {
            ThrowDuplicateOnFirstSave = true
        };
        var handler = new SendActivityWebhookHandler(
            sender,
            repository,
            unitOfWork,
            new ClockFake(Now));

        await handler.HandleAsync(CreateCommand());

        Assert.Null(sender.Payload);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_persists_error_and_rethrows_when_http_fails()
    {
        var repository = new WebhookDeliveryRepositoryFake();
        var sender = new WebhookSenderFake
        {
            ExceptionToThrow = new HttpRequestException(
                "Destination unavailable")
        };
        var unitOfWork = new UnitOfWorkFake();
        var handler = new SendActivityWebhookHandler(
            sender,
            repository,
            unitOfWork,
            new ClockFake(Now));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => handler.HandleAsync(CreateCommand()));

        Assert.Equal("Destination unavailable", exception.Message);
        var delivery = Assert.Single(repository.AddedDeliveries);
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(Now, delivery.LastAttemptAtUtc);
        Assert.Equal("Destination unavailable", delivery.LastError);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal(2, unitOfWork.SaveChangesCount);
    }

    private static SendActivityWebhookCommand CreateCommand(
        Guid? activityId = null) =>
        new(
            activityId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Follow up",
            Now);

    private sealed class WebhookSenderFake : IWebhookSender
    {
        public Exception? ExceptionToThrow { get; init; }

        public Guid? IdempotencyKey { get; private set; }

        public ActivityWebhookPayload? Payload { get; private set; }

        public Task SendAsync(
            Guid idempotencyKey,
            ActivityWebhookPayload payload,
            CancellationToken cancellationToken = default)
        {
            IdempotencyKey = idempotencyKey;
            Payload = payload;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class WebhookDeliveryRepositoryFake
        : IWebhookDeliveryRepository
    {
        public WebhookDelivery? ExistingDelivery { get; init; }

        public List<WebhookDelivery> AddedDeliveries { get; } = [];

        public Task<WebhookDelivery?> GetByActivityIdAsync(
            ActivityId activityId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ExistingDelivery);
        }

        public void Add(WebhookDelivery delivery)
        {
            AddedDeliveries.Add(delivery);
        }
    }

    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        public bool ThrowDuplicateOnFirstSave { get; init; }

        public int SaveChangesCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCount++;

            if (ThrowDuplicateOnFirstSave && SaveChangesCount == 1)
            {
                throw new DuplicateWebhookDeliveryException(
                    new InvalidOperationException());
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ClockFake(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
