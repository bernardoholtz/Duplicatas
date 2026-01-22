using CustomerPlatform.Application.Commands.AnalyzeDuplicate;
using MediatR;
using Notification.Domain.Interfaces;

namespace Duplicatas.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IRabbitMQ _rabbitMQ;

        public Worker(
           ILogger<Worker> logger,
           IServiceScopeFactory scopeFactory,
           IRabbitMQ rabbitMQ)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _rabbitMQ = rabbitMQ;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _rabbitMQ.StartConsuming(async (customerEvent) =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    await mediator.Send(new AnalyzeDuplicateCommand(customerEvent), stoppingToken);
                }
            });

            return Task.CompletedTask;
        }

    }
}
