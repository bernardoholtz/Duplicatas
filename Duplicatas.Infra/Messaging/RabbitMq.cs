using CustomerPlatform.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notification.Domain.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading;

public class RabbitMq : IRabbitMQ, IDisposable
{
    private IConnection _connection;
    private IModel _consumeChannel;
    private readonly ILogger<RabbitMq> _logger;
    private readonly IConfiguration _configuration;
    private readonly object _lock = new object();
    private const string queueNameCliente = "EventosCliente";
    private const string queueNameDuplicidade = "SuspeitosDuplicidadeCliente";

    public RabbitMq(IConfiguration configuration, ILogger<RabbitMq> logger)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private IConnection GetConnection()
    {
        if (_connection != null && _connection.IsOpen)
            return _connection;

        lock (_lock)
        {
            if (_connection != null && _connection.IsOpen)
                return _connection;

            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"],
                Port = int.Parse(_configuration["RabbitMQ:Port"]),
                UserName = _configuration["RabbitMQ:UserName"],
                Password = _configuration["RabbitMQ:Password"],
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            // Retry logic para aguardar o RabbitMQ estar pronto
            var maxRetries = 10;
            var delay = TimeSpan.FromSeconds(5);
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    _logger.LogInformation("Tentando conectar ao RabbitMQ (tentativa {Attempt}/{MaxRetries})...", i + 1, maxRetries);
                    _connection = factory.CreateConnection();
                    _logger.LogInformation("Conectado ao RabbitMQ com sucesso!");
                    return _connection;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao conectar ao RabbitMQ (tentativa {Attempt}/{MaxRetries}). Aguardando {Delay}s antes de tentar novamente...", 
                        i + 1, maxRetries, delay.TotalSeconds);
                    
                    if (i < maxRetries - 1)
                    {
                        Thread.Sleep(delay);
                    }
                    else
                    {
                        _logger.LogError(ex, "Não foi possível conectar ao RabbitMQ após {MaxRetries} tentativas", maxRetries);
                        throw;
                    }
                }
            }

            throw new InvalidOperationException("Não foi possível estabelecer conexão com o RabbitMQ");
        }
    }

    private IModel GetConsumeChannel()
    {
        if (_consumeChannel != null && _consumeChannel.IsOpen)
            return _consumeChannel;

        lock (_lock)
        {
            if (_consumeChannel != null && _consumeChannel.IsOpen)
                return _consumeChannel;

            var connection = GetConnection();
            _consumeChannel = connection.CreateModel();
            _consumeChannel.BasicQos(0, 1, false);

            _consumeChannel.QueueDeclare(queue: queueNameCliente, durable: true, exclusive: false, autoDelete: false);
            _consumeChannel.QueueDeclare(queue: queueNameDuplicidade, durable: true, exclusive: false, autoDelete: false);
            
            return _consumeChannel;
        }
    }

    public void StartConsuming(Func<CustomerEvent, Task> onMessageReceived)
    {
        var channel = GetConsumeChannel();
        var consumer = new EventingBasicConsumer(channel);

        consumer.Received += async (model, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<CustomerEvent>(json)!;

                await onMessageReceived(message);

                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no Handler.");
                channel.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        };

        channel.BasicConsume(queue: queueNameCliente, autoAck: false, consumer: consumer);
    }

    public async Task Send(CustomerEvent customerEvent)
    {
        if (customerEvent == null)
        {
            _logger.LogWarning("Tentativa de enviar evento nulo para a fila");
            throw new ArgumentNullException(nameof(customerEvent));
        }

        var connection = GetConnection();
        
        if (!connection.IsOpen)
        {
            _logger.LogError("Conexão RabbitMQ não está aberta ao tentar enviar mensagem. EventId: {EventId}", 
                customerEvent.EventId);
            throw new InvalidOperationException("Conexão RabbitMQ não está disponível");
        }

        IModel sendChannel = null;
        try
        {
            sendChannel = connection.CreateModel();
            
            string json;
            try
            {
                json = JsonSerializer.Serialize(customerEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao serializar evento. EventId: {EventId}, EventType: {EventType}", 
                    customerEvent.EventId, customerEvent.EventType);
                throw;
            }

            var body = Encoding.UTF8.GetBytes(json);
            var properties = sendChannel.CreateBasicProperties();
            properties.Persistent = true;

            try
            {
                sendChannel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: queueNameDuplicidade,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Evento enviado com sucesso para a fila. EventId: {EventId}, EventType: {EventType}", 
                    customerEvent.EventId, customerEvent.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao publicar mensagem no RabbitMQ. EventId: {EventId}", 
                    customerEvent.EventId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar evento para fila. EventId: {EventId}", customerEvent.EventId);
            throw;
        }
        finally
        {
            sendChannel?.Dispose();
        }
    }

    public void Dispose()
    {
        _consumeChannel?.Dispose();
        _connection?.Dispose();
    }
}