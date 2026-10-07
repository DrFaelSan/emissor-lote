using NEO_e.Domain.ValueObjects;

namespace NEO_e.Domain.Entities;

public enum TipoDocumento
{
    NfseEmitida,
    NfseRecebida,
    NfeEmitida,
    NfeRecebida,
    Evento
}

public sealed class DocumentoFiscal
{
    public ChaveAcesso ChaveAcesso { get; }
    public TipoDocumento Tipo { get; private set; }
    public Cnpj CnpjEmitente { get; }
    public Cnpj CnpjDestinatario { get; }
    public string Numero { get; }
    public string Serie { get; }
    public DateTimeOffset DataEmissao { get; }
    public DateTimeOffset? DataAutorizacao { get; }
    public ValorMonetario ValorTotal { get; }
    public string XmlContent { get; }
    public string? CaminhoArquivo { get; private set; }
    public DateTimeOffset DataProcessamento { get; }
    public Nsu Nsu { get; }
    public string HashXml { get; }

    public DocumentoFiscal(
        ChaveAcesso chaveAcesso,
        TipoDocumento tipo,
        Cnpj cnpjEmitente,
        Cnpj cnpjDestinatario,
        string numero,
        string serie,
        DateTimeOffset dataEmissao,
        DateTimeOffset? dataAutorizacao,
        ValorMonetario valorTotal,
        string xmlContent,
        Nsu nsu)
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
        Nsu = nsu;
        DataProcessamento = DateTimeOffset.UtcNow;
        HashXml = ComputeHash(xmlContent);
    }

    public void DefinirCaminhoArquivo(string caminho)
    {
        CaminhoArquivo = caminho;
    }

    public void ClassificarPara(Cnpj cnpjConsultado)
    {
        Tipo = TipoDocumentoClassifier.Classify(Tipo, CnpjEmitente, CnpjDestinatario, cnpjConsultado);
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

public static class TipoDocumentoClassifier
{
    public static TipoDocumento Classify(
        TipoDocumento tipo,
        Cnpj cnpjEmitente,
        Cnpj cnpjDestinatario,
        Cnpj cnpjConsultado)
    {
        var (emitida, recebida) = tipo switch
        {
            TipoDocumento.NfseEmitida or TipoDocumento.NfseRecebida => (TipoDocumento.NfseEmitida, TipoDocumento.NfseRecebida),
            TipoDocumento.NfeEmitida or TipoDocumento.NfeRecebida => (TipoDocumento.NfeEmitida, TipoDocumento.NfeRecebida),
            _ => throw new InvalidOperationException($"Tipo de documento nao classificavel: {tipo}")
        };

        var emitenteRelacionado = cnpjEmitente.Root == cnpjConsultado.Root;
        var destinatarioRelacionado = cnpjDestinatario.Root == cnpjConsultado.Root;

        if (emitenteRelacionado && !destinatarioRelacionado)
            return emitida;

        if (destinatarioRelacionado && !emitenteRelacionado)
            return recebida;

        if (emitenteRelacionado && destinatarioRelacionado)
        {
            var emitenteExato = cnpjEmitente == cnpjConsultado;
            var destinatarioExato = cnpjDestinatario == cnpjConsultado;

            if (emitenteExato)
                return emitida;
            if (destinatarioExato)
                return recebida;
        }

        throw new InvalidOperationException(
            $"Documento {tipo} nao pode ser classificado para o CNPJ consultado.");
    }
}