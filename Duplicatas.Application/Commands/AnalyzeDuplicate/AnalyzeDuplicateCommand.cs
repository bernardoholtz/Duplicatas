using CustomerPlatform.Domain.Events;
using MediatR;

namespace CustomerPlatform.Application.Commands.AnalyzeDuplicate
{
    public record AnalyzeDuplicateCommand(CustomerEvent EventData) : IRequest;
}