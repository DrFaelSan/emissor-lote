using NEO_e.Domain.ValueObjects;

namespace NEO_e.Domain.Entities;

public enum TipoDocumentoFiscal
{
    NfseRecebida,
    NfeRecebida
}

public enum StatusManifestacao
{
    Pendente,
    CienciaDaOperacao,
    ConfirmacaoDaOperacao,
    DesconhecimentoDaOperacao,
    OperacaoNaoRealizada,
    ErroEnvio,
    PrazoExpirado,
    Duplicidade
}

public sealed class DocumentoRecebido
{
    public ChaveAcesso ChaveAcesso { get; }
    public TipoDocumentoFiscal Tipo { get; }
    public Cnpj CnpjEmitente { get; }
    public Cnpj CnpjDestinatario { get; }
    public string Numero { get; }
    public string Serie { get; }
    public DateTimeOffset DataEmissao { get; }
    public DateTimeOffset? DataAutorizacao { get; }
    public ValorMonetario ValorTotal { get; }
    public string XmlContent { get; }
    public string? CaminhoArquivo { get; set; }
    public DateTimeOffset DataIndexacao { get; internal set; }
    public Nsu? Nsu { get; set; }
    public StatusManifestacao StatusManifestacao { get; set; }
    public DateTimeOffset? DataManifestacao { get; set; }
    public string? ProtocoloManifestacao { get; set; }
    public string? Justificativa { get; set; }
    public string HashXml { get; }

    public DocumentoRecebido(
        ChaveAcesso chaveAcesso,
        TipoDocumentoFiscal tipo,
        Cnpj cnpjEmitente,
        Cnpj cnpjDestinatario,
        string numero,
        string serie,
        DateTimeOffset dataEmissao,
        DateTimeOffset? dataAutorizacao,
        ValorMonetario valorTotal,
        string xmlContent)
    {
        ChaveAcesso = chaveAcesso;
        Tipo = tipo;
        CnpjEmitente = cnpjEmitente;
        CnpjDestinatario = cnpjDestinatario;
        Numero = numero;
        Serie = serie;
        DataEmissao = dataEmissao;
        DataAutorizacao = dataAutorizacao;
        ValorTotal = valorTotal;
        XmlContent = xmlContent;
        DataIndexacao = DateTimeOffset.UtcNow;
        StatusManifestacao = StatusManifestacao.Pendente;
        HashXml = ComputeHash(xmlContent);
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool ConteudoIgual(string outroXml) => ComputeHash(outroXml) == HashXml;
}