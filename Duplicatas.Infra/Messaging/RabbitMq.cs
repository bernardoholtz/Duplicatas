using CustomerPlatform.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notification.Domain.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Notification.Infra.Messaging
{
    public class RabbitMq : IRabbitMQ, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMq> _logger;
        private const string queueName = "EventosCliente";
        public RabbitMq(
            IConfiguration configuration,
            ILogger<IRabbitMQ> logger)
        {
            _logger = (ILogger<RabbitMq>?)(logger ?? throw new ArgumentNullException(nameof(logger)));

            var hostName = configuration["RabbitMQ:HostName"];
            var port = int.Parse(configuration["RabbitMQ:Port"]);
            var userName = configuration["RabbitMQ:UserName"];
            var password = configuration["RabbitMQ:Password"];

            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password,

            };

            try
            {
                _connection = factory.CreateConnection();
                _logger.LogInformation("Conexão com RabbitMQ estabelecida com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao conectar com RabbitMQ");
                throw;
            }
        }
        public async Task<CustomerEvent> Consume()
        {
            try
            {
                using var _channel = _connection.CreateModel();
                // Declara a fila se não existir
                _channel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var consumer = new EventingBasicConsumer(_channel);

                var message = new CustomerEvent();

                consumer.Received += async (_, ea) =>
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    message = JsonSerializer.Deserialize<CustomerEvent>(json)!;

                    //await _consumer.HandleAsync(message);

                    _channel.BasicAck(ea.DeliveryTag, false);
                };


                _channel.BasicConsume("member_queue", false, consumer);

                return message;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consumir a fila {QueueName}", queueName);
                throw;
            }
        }

        public void Dispose()
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }

        public async Task Send(CustomerEvent customerEvent)
        {
            try
            {
                using var _channel = _connection.CreateModel();
                // Declara a fila se não existir
                _channel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var json = JsonSerializer.Serialize(customerEvent);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true; // Garante que a mensagem será persistida

                _channel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Mensagem publicada na fila {QueueName}: {Message}", queueName, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao publicar mensagem na fila {QueueName}", queueName);
                throw;
            }
        }

    }

}
