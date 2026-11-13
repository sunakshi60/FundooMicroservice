using System.Text.Json;
using Confluent.Kafka;
using Fundoo.User.Application.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fundoo.User.Infrastructure.Messaging
{
    public class KafkaProducer
    {
        private readonly IProducer<string, string> _producer;
        private readonly IConfiguration _configuration;
        private readonly ILogger<KafkaProducer> _logger;

        public KafkaProducer(IConfiguration configuration,ILogger<KafkaProducer> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var config = new ProducerConfig
            {
                BootstrapServers = _configuration["Kafka:BootstrapServers"]
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public async Task PublishPasswordResetAsync(PasswordResetRequested passwordResetEvent,
            CancellationToken cancellationToken = default)
        {
            var topic = _configuration["Kafka:NotificationTopic"]
                        ?? "fundoo-notifications";

            var message = new Message<string, string>
            {
                Key = passwordResetEvent.EventId,
                Value = JsonSerializer.Serialize(passwordResetEvent)
            };

            try
            {
                var result = await _producer.ProduceAsync(
                    topic,
                    message,
                    cancellationToken);

                _logger.LogInformation(
                    "Password reset event {EventId} published to topic {Topic}, partition {Partition}",
                    passwordResetEvent.EventId,
                    topic,
                    result.Partition);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish password reset event {EventId}",
                    passwordResetEvent.EventId);

                throw;
            }
        }
    }
}
