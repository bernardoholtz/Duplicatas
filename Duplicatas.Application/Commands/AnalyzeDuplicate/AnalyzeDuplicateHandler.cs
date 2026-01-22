using CustomerPlatform.Application.Commands.AnalyzeDuplicate;
using CustomerPlatform.Domain.Entities;
using CustomerPlatform.Domain.Enums;
using CustomerPlatform.Domain.Events;
using CustomerPlatform.Domain.Interfaces;
using Duplicatas.Application.DTO;
using Duplicatas.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Nest;
using Notification.Domain.Interfaces;
using System.Text.Json;

public class AnalyzeDuplicateHandler : IRequestHandler<AnalyzeDuplicateCommand>
{
    private readonly IElasticClient _elasticClient;
    private readonly ISuspeitaDuplicidade _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRabbitMQ _rabbitMQ;
    private readonly ILogger<AnalyzeDuplicateHandler> _logger;
    private const string NomeEvento = "DuplicataSuspeita";

    public AnalyzeDuplicateHandler(IElasticClient elasticClient, 
        ISuspeitaDuplicidade repository, 
        IUnitOfWork unitOfWork,
        IRabbitMQ rabbitMQ,
        ILogger<AnalyzeDuplicateHandler> logger)
    {
        _elasticClient = elasticClient;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _rabbitMQ = rabbitMQ;
        _logger = logger;

    }

    public async Task Handle(AnalyzeDuplicateCommand request, CancellationToken ct)
    {
        if (request?.EventData?.Data == null)
        {
            _logger.LogWarning("Comando recebido com dados inválidos ou nulos");
            return;
        }

        try
        {
            var original = request.EventData.Data;

            if (original.ClienteId == Guid.Empty)
            {
                _logger.LogWarning("ClienteId inválido no evento. EventId: {EventId}", request.EventData.EventId);
                return;
            }

            string usernameOriginal = original.Email?.Split('@')[0] ?? string.Empty;

            ISearchResponse<CustomerSearchDto> searchResponse;
            try
            {
                searchResponse = await _elasticClient.SearchAsync<CustomerSearchDto>(s => s
                    .Index("customers")
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                sh => sh.Match(m => m.Field(f => f.Nome).Query(original.Nome).Fuzziness(Fuzziness.Auto)),
                                sh => sh.Match(m => m.Field(f => f.Documento).Query(original.Documento)),

                                sh => sh.Match(m => m.Field(f => f.Email)
                                    .Query(usernameOriginal)
                                    .Fuzziness(Fuzziness.Auto)
                                    .PrefixLength(3) 
                                    .Boost(1.5)),

                                sh => sh.Term(t => t.Field("telefone.keyword").Value(original.Telefone).Boost(2.0))
                            )
                            .MustNot(m => m.Term(t => t.Field("id").Value(original.ClienteId)))
                        )
                    )
                    .Highlight(h => h
                        .Fields(
                            f => f.Field(fname => fname.Nome),
                            f => f.Field(fname => fname.Email),
                            f => f.Field(fname => fname.Documento),
                            f => f.Field("telefone.keyword")
                        )
                        .PreTags("").PostTags("")
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar no Elasticsearch. ClienteId: {ClienteId}, EventId: {EventId}", 
                    original.ClienteId, request.EventData.EventId);
                throw;
            }

            if (!searchResponse.IsValid)
            {
                _logger.LogWarning("Resposta inválida do Elasticsearch. ClienteId: {ClienteId}, Erro: {Erro}", 
                    original.ClienteId, searchResponse.ServerError?.Error?.Reason);
                return;
            }

            foreach (var hit in searchResponse.Hits)
            {
                try
                {
                    if (hit.Score > 4)
                    {
                        var duplicado = hit.Source;
                        if (duplicado == null)
                        {
                            _logger.LogWarning("Hit com score > 4 mas Source é nulo. Score: {Score}", hit.Score);
                            continue;
                        }

                        var comparativoList = new List<(string Campo, string ValorOriginal, string ValorEncontrado, double? ScoreCampo)>();
                        
                        if (hit.Highlight != null)
                        {
                            comparativoList = hit.Highlight.Select(h => (
                                Campo: h.Key,
                                ValorOriginal: ObterValorPorCampo(original, h.Key),
                                ValorEncontrado: string.Join(", ", h.Value),
                                ScoreCampo: hit.Score
                            )).ToList();
                        }

                        if (!ValidarUsernameSimilar(original.Email, duplicado.Email))
                        {
                            comparativoList = comparativoList.Where(x => x.Campo.ToLower() != "email").ToList();
                        }

                        if (comparativoList.Any())
                        {
                            var suspeita = new SuspeitaDuplicidade
                            {
                                IdOriginal = original.ClienteId,
                                IdSuspeito = duplicado.Id,
                                Score = hit.Score ?? 0,
                                DetalhesSimilaridade = JsonSerializer.Serialize(new
                                {
                                    Resumo = $"Comparação entre Novo Registro ({original.Nome}) e Existente ({duplicado.Nome})",
                                    ComparativoDetalhado = comparativoList.Select(c => new
                                    {
                                        c.Campo,
                                        c.ValorOriginal,
                                        c.ValorEncontrado,
                                        c.ScoreCampo
                                    }), 
                                    ScoreGlobal = hit.Score
                                }),
                                DataDeteccao = DateTime.UtcNow
                            };

                            try
                            {
                                await _repository.Add(suspeita);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Erro ao adicionar suspeita de duplicidade. IdOriginal: {IdOriginal}, IdSuspeito: {IdSuspeito}", 
                                    suspeita.IdOriginal, suspeita.IdSuspeito);
                                throw;
                            }

                            try
                            {
                                var evento = CreateEvent(duplicado);
                                await _rabbitMQ.Send(evento);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Erro ao enviar evento para fila. IdSuspeito: {IdSuspeito}", duplicado.Id);
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar hit do Elasticsearch. ClienteId: {ClienteId}, Score: {Score}", 
                        original.ClienteId, hit.Score);
                    // Continua processando os próximos hits mesmo se um falhar
                }
            }

            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao fazer commit no UnitOfWork. ClienteId: {ClienteId}", original.ClienteId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar comando AnalyzeDuplicate. EventId: {EventId}", 
                request?.EventData?.EventId);
            throw;
        }
    }

    private bool ValidarUsernameSimilar(string emailOrig, string emailDupl)
    {
        if (string.IsNullOrEmpty(emailOrig) || string.IsNullOrEmpty(emailDupl)) 
            return false;

        try
        {
            if (!emailOrig.Contains('@') || !emailDupl.Contains('@'))
                return false;

            var userOrig = emailOrig.Split('@')[0].ToLower();
            var userDupl = emailDupl.Split('@')[0].ToLower();

            if (string.IsNullOrEmpty(userOrig) || string.IsNullOrEmpty(userDupl))
                return false;

            if (userOrig == userDupl) 
                return true;

            if (userOrig.Length >= 3 && userDupl.Length >= 3)
            {
                if (userOrig[..3] != userDupl[..3]) 
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao validar username similar. EmailOrig: {EmailOrig}, EmailDupl: {EmailDupl}", 
                emailOrig, emailDupl);
            return false;
        }
    }

    private string ObterValorPorCampo(CustomerEventData data, string fieldName)
    {
        if (data == null || string.IsNullOrEmpty(fieldName))
            return "N/A";

        try
        {
            return fieldName.ToLower() switch
            {
                "nome" => data.Nome ?? "N/A",
                "email" => data.Email ?? "N/A",
                "documento" => data.Documento ?? "N/A",
                "telefone.keyword" => data.Telefone ?? "N/A",
                "telefone" => data.Telefone ?? "N/A",
                _ => "N/A"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao obter valor por campo. FieldName: {FieldName}", fieldName);
            return "N/A";
        }
    }

    private CustomerEvent CreateEvent(CustomerSearchDto customerSearchDto)
    {
        if (customerSearchDto == null)
        {
            _logger.LogWarning("Tentativa de criar evento com CustomerSearchDto nulo");
            throw new ArgumentNullException(nameof(customerSearchDto));
        }

        var evento = new CustomerEvent
        {
            EventId = Guid.NewGuid(),
            EventType = NomeEvento,
            Timestamp = DateTime.UtcNow,
            Data = new CustomerEventData
            {
                ClienteId = customerSearchDto.Id,
                Telefone = customerSearchDto.Telefone,
                Email = customerSearchDto.Email,
            }
        };

        switch (customerSearchDto.TipoCliente)
        {
            case TipoCliente.PessoaFisica :
                evento.Data.TipoCliente = "PF";
                evento.Data.Nome = customerSearchDto.Nome;
                evento.Data.Documento = customerSearchDto.Documento;
                break;

            case TipoCliente.PessoaJuridica :
                evento.Data.TipoCliente = "PJ";
                evento.Data.Nome = customerSearchDto.Nome;
                evento.Data.Documento = customerSearchDto.Documento;
                break;

            default:
                _logger.LogWarning("Tipo de cliente desconhecido: {TipoCliente}. ClienteId: {ClienteId}", 
                    customerSearchDto.TipoCliente, customerSearchDto.Id);
                break;
        }

        return evento;
    }

}