using Confluent.Kafka;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.DTO;
using Fundoo.User.Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using Fundoo.Observability;

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
            await Task.Yield();

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

                    var parentContext = FundooObservability.ExtractTraceContext(
                        result.Message.Headers,
                        static (headers, key) => headers is null
                            ? Enumerable.Empty<string>()
                            : headers
                                .Where(header => string.Equals(header.Key, key, StringComparison.OrdinalIgnoreCase))
                                .Select(header => Encoding.UTF8.GetString(header.GetValueBytes())));
                    using var activity = FundooObservability.StartKafkaProcess(topic, parentContext);

                    try
                    {

                    using var document =
                        JsonDocument.Parse(result.Message.Value);

                    var root = document.RootElement;

                    if (root.TryGetProperty("Otp", out _))
                    {
                        var message =
                            JsonSerializer.Deserialize<LoginOtpRequestedMessage>(
                                result.Message.Value);

                        if (message == null)
                        {
                            _logger.LogWarning(
                                "Invalid login OTP event received.");

                            consumer.Commit(result);
                            continue;
                        }

                        await _notificationService.SendLoginOtpNotificationAsync(
                            new LoginOtpNotificationRequest
                            {
                                RecipientEmail = message.Email,
                                RecipientName = message.UserName,
                                Otp = message.Otp,
                                ExpiresAtUtc = message.ExpiresAtUtc
                            });

                        consumer.Commit(result);

                        _logger.LogInformation(
                            "Login OTP email processed for event {EventId}",
                            message.EventId);
                    }
                    else if (root.TryGetProperty("ResetToken", out _))
                    {
                        var message =
                            JsonSerializer.Deserialize<PasswordResetRequestedMessage>(
                                result.Message.Value);

                        if (message == null)
                        {
                            _logger.LogWarning(
                                "Invalid password reset event received.");

                            consumer.Commit(result);
                            continue;
                        }

                        await _notificationService.SendPasswordResetNotificationAsync(
                            new PasswordResetNotificationRequest
                            {
                                RecipientEmail = message.Email,
                                RecipientName = message.UserName,
                                ResetToken = message.ResetToken
                            });

                        consumer.Commit(result);

                        _logger.LogInformation(
                            "Password reset email processed for event {EventId}",
                            message.EventId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Unknown notification event received.");

                        consumer.Commit(result);
                    }
                    }
                    catch (Exception exception)
                    {
                        FundooObservability.RecordFailure(activity, exception);
                        throw;
                    }
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
