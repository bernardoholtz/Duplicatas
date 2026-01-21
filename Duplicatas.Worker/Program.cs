using CustomerPlatform.Domain.Interfaces;
using CustomerPlatform.Infrastructure.Repositories;
using Duplicatas.Domain.Interfaces;
using Duplicatas.Infra.Repositories;
using Duplicatas.Worker;
using Notification.Domain.Interfaces;
using Notification.Infra.Messaging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<ISuspeitaDuplicidade, SuspeitaDuplicidadeRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<IRabbitMQ, RabbitMq>();

var host = builder.Build();
host.Run();
