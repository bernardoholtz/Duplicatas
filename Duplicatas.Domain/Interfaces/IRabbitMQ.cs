using CustomerPlatform.Domain.Events;

namespace Notification.Domain.Interfaces
{
    public interface IRabbitMQ
    {
        Task<CustomerEvent> Consume();
        Task Send(CustomerEvent customerEvent);
    }
}
