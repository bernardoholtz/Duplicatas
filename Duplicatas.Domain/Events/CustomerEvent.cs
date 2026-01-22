namespace CustomerPlatform.Domain.Events
{
    public class CustomerEvent
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public CustomerEventData Data { get; set; }
    }

    public class CustomerEventData
    {
        public Guid ClienteId { get; set; }
        public string TipoCliente { get; set; }
        public string Documento { get; set; }
        public string Nome { get; set; }
        public string Telefone { get; set; }
        public string Email { get; set; }
    }
}
