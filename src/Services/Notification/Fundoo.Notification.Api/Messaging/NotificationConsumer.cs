using Confluent.Kafka;
using Fundoo.Notification.Application;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.DTO;
using Fundoo.User.Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Fundoo.Notification.Api.Messaging
{
    public class NotificationConsumer : BackgroundService
    {
        private readonly ILogger<NotificationConsumer> _logger;
        private readonly INotificationService _notificationService;
        private readonly IConfiguration _configuration;

        public NotificationConsumer(
            ILogger<NotificationConsumer> logger,
            INotificationService notificationService,
            IConfiguration configuration)
        {
            _logger = logger;
            _notificationService = notificationService;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers =
                    _configuration["Kafka:BootstrapServers"],

                GroupId = "fundoo-notification-group",

                AutoOffsetReset = AutoOffsetReset.Earliest,

                EnableAutoCommit = false
            };

            using var consumer =
                new ConsumerBuilder<string, string>(config).Build();

            var topic = _configuration["Kafka:NotificationTopic"]
                        ?? "fundoo-notifications";

            consumer.Subscribe(topic);

            _logger.LogInformation(
                "Notification consumer started. Listening to {Topic}",
                topic);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = consumer.Consume(stoppingToken);

                    var message = JsonSerializer.Deserialize<PasswordResetRequestedMessage>(
                            result.Message.Value);

                    if (message == null)
                    {
                        _logger.LogWarning(
                            "Received an empty or invalid notification event.");

                        consumer.Commit(result);
                        continue;
                    }

                    _logger.LogInformation(
                        "Received password reset event {EventId}",
                        message.EventId);

                    // Call your existing email service.

                    var notificationRequest = new PasswordResetNotificationRequest
                    {
                        RecipientEmail = message.Email,
                        RecipientName = message.UserName,
                        ResetToken = message.ResetToken
                    };

                    await _notificationService.SendPasswordResetNotificationAsync(
                        notificationRequest);


                    // Commit only after email service succeeds.
                    consumer.Commit(result);

                    _logger.LogInformation(
                        "Password reset email processed for event {EventId}",
                        message.EventId);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Notification consumer is stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Notification consumer stopped because message processing failed.");

                throw;
            }
            finally
            {
                consumer.Close();
            }
        }
    }
}
