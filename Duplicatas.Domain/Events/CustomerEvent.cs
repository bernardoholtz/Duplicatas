namespace CustomerPlatform.Domain.Events
{
    /// <summary>
    /// Evento genérico para operações de Customer (Criado ou Atualizado)
    /// </summary>
    public class CustomerEvent
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public CustomerEventData Data { get; set; }
    }

    /// <summary>
    /// Dados do evento de Customer
    /// </summary>
    public class CustomerEventData
    {
        public Guid ClienteId { get; set; }
        public string TipoCliente { get; set; }
        public string Documento { get; set; }
        public string Nome { get; set; }
    }
}
