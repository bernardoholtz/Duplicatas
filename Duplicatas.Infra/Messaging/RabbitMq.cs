using CustomerPlatform.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notification.Domain.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

public class RabbitMq : IRabbitMQ, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _consumeChannel;
    private readonly ILogger<RabbitMq> _logger;
    private const string queueNameCliente = "EventosCliente";
    private const string queueNameDuplicidade = "SuspeitosDuplicidadeCliente";

    public RabbitMq(IConfiguration configuration, ILogger<RabbitMq> logger)
    {
        _logger = logger;
        var factory = new ConnectionFactory

        {

            HostName = configuration["RabbitMQ:HostName"],

            Port = int.Parse(configuration["RabbitMQ:Port"]),

            UserName = configuration["RabbitMQ:UserName"],

            Password = configuration["RabbitMQ:Password"],

            AutomaticRecoveryEnabled = true,

            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)

        };

        _connection = factory.CreateConnection();
        _consumeChannel = _connection.CreateModel();
        _consumeChannel.BasicQos(0, 1, false);

        _consumeChannel.QueueDeclare(queue: queueNameCliente, durable: true, exclusive: false, autoDelete: false);
        _consumeChannel.QueueDeclare(queue: queueNameDuplicidade, durable: true, exclusive: false, autoDelete: false);
    }

    public void StartConsuming(Func<CustomerEvent, Task> onMessageReceived)
    {
        var consumer = new EventingBasicConsumer(_consumeChannel);

        consumer.Received += async (model, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<CustomerEvent>(json)!;

                await onMessageReceived(message);

                _consumeChannel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no Handler.");
                _consumeChannel.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        };

        _consumeChannel.BasicConsume(queue: queueNameCliente, autoAck: false, consumer: consumer);
    }

    public async Task Send(CustomerEvent customerEvent)
    {
        using var sendChannel = _connection.CreateModel();

        var json = JsonSerializer.Serialize(customerEvent);
        var body = Encoding.UTF8.GetBytes(json);
        var properties = sendChannel.CreateBasicProperties();
        properties.Persistent = true;

        sendChannel.BasicPublish(
            exchange: string.Empty,
            routingKey: queueNameDuplicidade,
            basicProperties: properties,
            body: body);
    }

    public void Dispose()
    {
        _consumeChannel?.Dispose();
        _connection?.Dispose();
    }
}