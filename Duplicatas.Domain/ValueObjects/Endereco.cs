namespace CustomerPlatform.Domain.ValueObjects;

/// <summary>
/// Endereço do cliente (Value Object)
/// </summary>
public class Endereco
{
    public string Logradouro { get; }
    public string Numero { get; }
    public string? Complemento { get; }
    public string CEP { get; }
    public string Cidade { get; }
    public string Estado { get; }

    protected Endereco () { }
    public Endereco(
        string logradouro,
        string numero,
        string cep,
        string cidade,
        string estado,
        string? complemento = null)
    {
        Logradouro = logradouro;
        Numero = numero;
        CEP = cep;
        Cidade = cidade;
        Estado = estado;
        Complemento = complemento;
    }
}
