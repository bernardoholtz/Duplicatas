using CustomerPlatform.Domain.ValueObjects;
using System.Text.Json.Serialization;
namespace CustomerPlatform.Domain.Entities;

[JsonDerivedType(typeof(ClientePessoaFisica), typeDiscriminator: "PF")]
[JsonDerivedType(typeof(ClientePessoaJuridica), typeDiscriminator: "PJ")]
public abstract class Customer
{
    public Guid Id { get; }
    public string Email { get; private set; }
    public string Telefone { get; private set; }
    public DateTime DataCriacao { get; }
    public DateTime? DataAtualizacao { get; private set; }
    public Endereco Endereco { get; private set; }

    protected Customer() { }
    protected Customer(
        string email,
        string telefone,
        Endereco endereco)
    {
        Id = Guid.NewGuid();
        Email = email;
        Telefone = telefone;
        Endereco = endereco;
        DataCriacao = DateTime.UtcNow;
    }

    public abstract string GetDocumento();
    public abstract string GetNome();
    public abstract bool ValidarDocumento();
    protected void AtualizarCustomer(string email,
        string telefone,
        Endereco endereco)
    {
        Email = email;
        Telefone = telefone;
        Endereco = endereco;
        DataAtualizacao = DateTime.UtcNow;
    }
}
