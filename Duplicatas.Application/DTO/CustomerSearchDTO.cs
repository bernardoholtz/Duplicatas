using CustomerPlatform.Domain.Enums;

namespace Duplicatas.Application.DTO
{
    public class CustomerSearchDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; }
        public string Documento { get; set; }
        public string Email { get; set; }
        public string Telefone { get; set; }
        public TipoCliente TipoCliente { get; set; }
    }
}
