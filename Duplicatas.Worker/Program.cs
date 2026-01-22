using CustomerPlatform.Application.Commands.AnalyzeDuplicate;
using CustomerPlatform.Domain.Interfaces;
using CustomerPlatform.Infrastructure.Contexts;
using CustomerPlatform.Infrastructure.Repositories;
using Duplicatas.Domain.Interfaces;
using Duplicatas.Infra.Repositories;
using Duplicatas.Worker;
using Elasticsearch.Net;
using Microsoft.EntityFrameworkCore;
using Nest;
using Notification.Domain.Interfaces;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<ISuspeitaDuplicidade, SuspeitaDuplicidadeRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<IRabbitMQ, RabbitMq>();
builder.Services.AddDbContext<CustomerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

var elasticUri = builder.Configuration.GetValue<string>("Elasticsearch:Uri");
var defaultIndex = builder.Configuration.GetValue<string>("Elasticsearch:DefaultIndex") ?? "customers";
var settings = new ConnectionSettings(new Uri(elasticUri))
    .DefaultIndex(defaultIndex) 
    .ServerCertificateValidationCallback(CertificateValidations.AllowAll)
    .SkipDeserializationForStatusCodes();
    


var client = new ElasticClient(settings);

builder.Services.AddSingleton<IElasticClient>(client);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(AnalyzeDuplicateCommand).Assembly);

    cfg.RegisterServicesFromAssembly(typeof(AnalyzeDuplicateHandler).Assembly);
});

var host = builder.Build();
host.Run();
