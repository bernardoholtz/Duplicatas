using CustomerPlatform.Application.Commands.AnalyzeDuplicate;
using CustomerPlatform.Domain.Entities;
using CustomerPlatform.Domain.Enums;
using CustomerPlatform.Domain.Events;
using CustomerPlatform.Domain.Interfaces;
using Duplicatas.Application.DTO;
using Duplicatas.Domain.Interfaces;
using MediatR;
using Nest;
using Notification.Domain.Interfaces;
using System.Text.Json;

public class AnalyzeDuplicateHandler : IRequestHandler<AnalyzeDuplicateCommand>
{
    private readonly IElasticClient _elasticClient;
    private readonly ISuspeitaDuplicidade _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRabbitMQ _rabbitMQ;
    private const string NomeEvento = "DuplicataSuspeita";

    public AnalyzeDuplicateHandler(IElasticClient elasticClient, 
        ISuspeitaDuplicidade repository, 
        IUnitOfWork unitOfWork,
        IRabbitMQ rabbitMQ)
    {
        _elasticClient = elasticClient;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _rabbitMQ = rabbitMQ;

    }

    public async Task Handle(AnalyzeDuplicateCommand request, CancellationToken ct)
    {
        var original = request.EventData.Data;

        string usernameOriginal = original.Email?.Split('@')[0] ?? string.Empty;

        var searchResponse = await _elasticClient.SearchAsync<CustomerSearchDto>(s => s
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

        if (!searchResponse.IsValid) return;

        foreach (var hit in searchResponse.Hits)
        {
            if (hit.Score > 4)
            {
                var duplicado = hit.Source;
                var comparativoList = hit.Highlight.Select(h => new
                {
                    Campo = h.Key,
                    ValorOriginal = ObterValorPorCampo(original, h.Key),
                    ValorEncontrado = string.Join(", ", h.Value),
                    ScoreCampo = hit.Score
                }).ToList();

                if (!ValidarUsernameSimilar(original.Email, duplicado.Email))
                {
                    comparativoList.RemoveAll(x => x.Campo.ToLower() == "email");
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
                            ComparativoDetalhado = comparativoList, 
                            ScoreGlobal = hit.Score
                        }),
                        DataDeteccao = DateTime.UtcNow
                    };

                    await _repository.Add(suspeita);

                    var evento = CreateEvent(hit.Source);
                    await _rabbitMQ.Send(evento);
                }
            }
        }
        await _unitOfWork.CommitAsync();

    }

    private bool ValidarUsernameSimilar(string emailOrig, string emailDupl)
    {
        if (string.IsNullOrEmpty(emailOrig) || string.IsNullOrEmpty(emailDupl)) return false;

        var userOrig = emailOrig.Split('@')[0].ToLower();
        var userDupl = emailDupl.Split('@')[0].ToLower();

        if (userOrig == userDupl) return true;

        if (userOrig.Length >= 3 && userDupl.Length >= 3)
        {
            if (userOrig[..3] != userDupl[..3]) return false;
        }

        return true;
    }

    private string ObterValorPorCampo(CustomerEventData data, string fieldName)
    {
        return fieldName.ToLower() switch
        {
            "nome" => data.Nome,
            "email" => data.Email,
            "documento" => data.Documento,
            "telefone.keyword" => data.Telefone,
            "telefone" => data.Telefone,
            _ => "N/A"
        };
    }

    private CustomerEvent CreateEvent(CustomerSearchDto customerSearchDto)
    {
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
        }

        return evento;
    }

}