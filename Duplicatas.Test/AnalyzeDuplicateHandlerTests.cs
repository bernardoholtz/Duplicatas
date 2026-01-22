using CustomerPlatform.Application.Commands.AnalyzeDuplicate;
using CustomerPlatform.Domain.Entities;
using CustomerPlatform.Domain.Enums;
using CustomerPlatform.Domain.Events;
using CustomerPlatform.Domain.Interfaces;
using Duplicatas.Application.DTO;
using Duplicatas.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Nest;
using Notification.Domain.Interfaces;

namespace Duplicatas.Test
{
    public class AnalyzeDuplicateHandlerTests
    {
        private readonly Mock<IElasticClient> _elasticClientMock;
        private readonly Mock<ISuspeitaDuplicidade> _repositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IRabbitMQ> _rabbitMQMock;
        private readonly Mock<ILogger<AnalyzeDuplicateHandler>> _loggerMock;
        private readonly AnalyzeDuplicateHandler _handler;

        public AnalyzeDuplicateHandlerTests()
        {
            _elasticClientMock = new Mock<IElasticClient>();
            _repositoryMock = new Mock<ISuspeitaDuplicidade>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _rabbitMQMock = new Mock<IRabbitMQ>();
            _loggerMock = new Mock<ILogger<AnalyzeDuplicateHandler>>();
            _handler = new AnalyzeDuplicateHandler(
                _elasticClientMock.Object,
                _repositoryMock.Object,
                _unitOfWorkMock.Object,
                _rabbitMQMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_DeveRetornarQuandoEventDataENulo()
        {
            // Arrange
            var command = new AnalyzeDuplicateCommand(null);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _elasticClientMock.Verify(x => x.SearchAsync<CustomerSearchDto>(
                It.IsAny<Func<SearchDescriptor<CustomerSearchDto>, ISearchRequest>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeveRetornarQuandoClienteIdEInvalido()
        {
            // Arrange
            var eventData = new CustomerEvent
            {
                EventId = Guid.NewGuid(),
                EventType = "TestEvent",
                Timestamp = DateTime.UtcNow,
                Data = new CustomerEventData
                {
                    ClienteId = Guid.Empty,
                    Nome = "Teste"
                }
            };
            var command = new AnalyzeDuplicateCommand(eventData);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _elasticClientMock.Verify(x => x.SearchAsync<CustomerSearchDto>(
                It.IsAny<Func<SearchDescriptor<CustomerSearchDto>, ISearchRequest>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeveBuscarNoElasticsearch()
        {
            // Arrange
            var clienteId = Guid.NewGuid();
            var eventData = new CustomerEvent
            {
                EventId = Guid.NewGuid(),
                EventType = "TestEvent",
                Timestamp = DateTime.UtcNow,
                Data = new CustomerEventData
                {
                    ClienteId = clienteId,
                    Nome = "João Silva",
                    Email = "joao@teste.com",
                    Documento = "12345678900",
                    Telefone = "11999999999"
                }
            };
            var command = new AnalyzeDuplicateCommand(eventData);

            var searchResponseMock = new Mock<ISearchResponse<CustomerSearchDto>>();
            searchResponseMock.Setup(x => x.IsValid).Returns(true);
            searchResponseMock.Setup(x => x.Hits).Returns(new List<IHit<CustomerSearchDto>>());

            _elasticClientMock.Setup(x => x.SearchAsync<CustomerSearchDto>(
                It.IsAny<Func<SearchDescriptor<CustomerSearchDto>, ISearchRequest>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResponseMock.Object);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _elasticClientMock.Verify(x => x.SearchAsync<CustomerSearchDto>(
                It.IsAny<Func<SearchDescriptor<CustomerSearchDto>, ISearchRequest>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_DeveRetornarQuandoRespostaDoElasticNaoEValida()
        {
            // Arrange
            var clienteId = Guid.NewGuid();
            var eventData = new CustomerEvent
            {
                EventId = Guid.NewGuid(),
                EventType = "TestEvent",
                Timestamp = DateTime.UtcNow,
                Data = new CustomerEventData
                {
                    ClienteId = clienteId,
                    Nome = "João Silva",
                    Email = "joao@teste.com",
                    Documento = "12345678900",
                    Telefone = "11999999999"
                }
            };
            var command = new AnalyzeDuplicateCommand(eventData);

            var searchResponseMock = new Mock<ISearchResponse<CustomerSearchDto>>();
            searchResponseMock.Setup(x => x.IsValid).Returns(false);

            _elasticClientMock.Setup(x => x.SearchAsync<CustomerSearchDto>(
                It.IsAny<Func<SearchDescriptor<CustomerSearchDto>, ISearchRequest>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResponseMock.Object);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _repositoryMock.Verify(x => x.Add(It.IsAny<SuspeitaDuplicidade>()), Times.Never);
            _rabbitMQMock.Verify(x => x.Send(It.IsAny<CustomerEvent>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeveProcessarHitComScoreMaiorQue4()
        {
            // Arrange
            var clienteId = Guid.NewGuid();
            var suspeitoId = Guid.NewGuid();
            var eventData = new CustomerEvent
            {
                EventId = Guid.NewGuid(),
                EventType = "TestEvent",
                Timestamp = DateTime.UtcNow,
                Data = new CustomerEventData
                {
                    ClienteId = clienteId,
                    Nome = "João Silva",
                    Email = "joao@teste.com",
                    Documento = "12345678900",
                    Telefone = "11999999999"
                }
            };
            var command = new AnalyzeDuplicateCommand(eventData);

            var duplicado = new CustomerSearchDto
            {
                Id = suspeitoId,
                Nome = "João Silva",
                Email = "joao.silva@teste.com",
                Documento = "12345678900",
                Telefone = "11999999999",
                TipoCliente = TipoCliente.PessoaFisica
            };

            var hitMock = new Mock<IHit<CustomerSearchDto>>();
            hitMock.Setup(x => x.Score).Returns(5.0);
            hitMock.Setup(x => x.Source).Returns(duplicado);
            hitMock.Setup(x => x.Highlight).Returns(new Dictionary<string, IReadOnlyCollection<string>>
            {
                { "nome", new List<string> { "João Silva" } },
                { "documento", new List<string> { "12345678900" } }
            });

            var searchResponseMock = new Mock<ISearchResponse<CustomerSearchDto>>();
            searchResponseMock.Setup(x => x.IsValid).Returns(true);
            searchResponseMock.Setup(x => x.Hits).Returns(new List<IHit<CustomerSearchDto>> { hitMock.Object });

            _elasticClientMock.Setup(x => x.SearchAsync<CustomerSearchDto>(
                It.IsAny<Func<SearchDescriptor<CustomerSearchDto>, ISearchRequest>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResponseMock.Object);

            _repositoryMock.Setup(x => x.Add(It.IsAny<SuspeitaDuplicidade>())).Returns(Task.CompletedTask);
            _rabbitMQMock.Setup(x => x.Send(It.IsAny<CustomerEvent>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(x => x.CommitAsync()).ReturnsAsync(1);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _repositoryMock.Verify(x => x.Add(It.Is<SuspeitaDuplicidade>(
                s => s.IdOriginal == clienteId && s.IdSuspeito == suspeitoId && s.Score == 5.0)), Times.Once);
            _rabbitMQMock.Verify(x => x.Send(It.IsAny<CustomerEvent>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_NaoDeveProcessarHitComScoreMenorOuIgualA4()
        {
            // Arrange
            var clienteId = Guid.NewGuid();
            var eventData = new CustomerEvent
            {
                EventId = Guid.NewGuid(),
                EventType = "TestEvent",
                Timestamp = DateTime.UtcNow,
                Data = new CustomerEventData
                {
                    ClienteId = clienteId,
                    Nome = "João Silva",
                    Email = "joao@teste.com",
                    Documento = "12345678900",
                    Telefone = "11999999999"
                }
            };
            var command = new AnalyzeDuplicateCommand(eventData);

            var hitMock = new Mock<IHit<CustomerSearchDto>>();
            hitMock.Setup(x => x.Score).Returns(3.0);
            hitMock.Setup(x => x.Source).Returns(new CustomerSearchDto());

            var searchResponseMock = new Mock<ISearchResponse<CustomerSearchDto>>();
            searchResponseMock.Setup(x => x.IsValid).Returns(true);
            searchResponseMock.Setup(x => x.Hits).Returns(new List<IHit<CustomerSearchDto>> { hitMock.Object });

            _elasticClientMock.Setup(x => x.SearchAsync<CustomerSearchDto>(
                It.IsAny<Func<SearchDescriptor<CustomerSearchDto>, ISearchRequest>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResponseMock.Object);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _repositoryMock.Verify(x => x.Add(It.IsAny<SuspeitaDuplicidade>()), Times.Never);
            _rabbitMQMock.Verify(x => x.Send(It.IsAny<CustomerEvent>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeveLancarExcecaoQuandoElasticsearchFalhar()
        {
            // Arrange
            var clienteId = Guid.NewGuid();
            var eventData = new CustomerEvent
            {
                EventId = Guid.NewGuid(),
                EventType = "TestEvent",
                Timestamp = DateTime.UtcNow,
                Data = new CustomerEventData
                {
                    ClienteId = clienteId,
                    Nome = "João Silva",
                    Email = "joao@teste.com",
                    Documento = "12345678900",
                    Telefone = "11999999999"
                }
            };
            var command = new AnalyzeDuplicateCommand(eventData);

            _elasticClientMock.Setup(x => x.SearchAsync<CustomerSearchDto>(
                It.IsAny<Func<SearchDescriptor<CustomerSearchDto>, ISearchRequest>>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Erro no Elasticsearch"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(async () => await _handler.Handle(command, CancellationToken.None));

            _loggerMock.Verify(
                x => x.Log(
                    Microsoft.Extensions.Logging.LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Erro ao buscar no Elasticsearch")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        
        
    }
}
