namespace CustomerPlatform.Domain.Entities
{
    public class SuspeitaDuplicidade
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid IdOriginal { get; set; }
        public Guid IdSuspeito { get; set; }
        public double Score { get; set; }

        public string DetalhesSimilaridade { get; set; }

        public DateTime DataDeteccao { get; set; } = DateTime.UtcNow;
    }
}
