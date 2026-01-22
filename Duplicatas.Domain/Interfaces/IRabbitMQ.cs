using CustomerPlatform.Domain.Events;

namespace Notification.Domain.Interfaces
{
    public interface IRabbitMQ
    {
        void StartConsuming(Func<CustomerEvent, Task> onMessageReceived);
        Task Send(CustomerEvent customerEvent);
    }
}
