using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NEO_e.Application.Contracts;
using NEO_e.Domain.ValueObjects;
using NEO_e.Domain.Exceptions;
using NEO_e.Infrastructure.Configuration;

namespace NEO_e.Infrastructure.Http;

public sealed class SefazManifestationClient : IManifestationClient
{
    private readonly SefazSettings _settings;
    private readonly NEO_e.Application.Contracts.ILogger _logger;
    private readonly HttpClient _httpClient;

    public SefazManifestationClient(IOptions<SefazSettings> settings, NEO_e.Application.Contracts.ILogger logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds) };
    }

    public async Task<ManifestationResult> SendEventAsync(ManifestationEvent evento, X509Certificate2 certificate, CancellationToken ct)
    {
        var soapEnvelope = BuildSoapEnvelope(evento, certificate);
        
        var request = new HttpRequestMessage(HttpMethod.Post, _settings.RecepcaoEventoUrl)
        {
            Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml")
        };
        request.Headers.Add("SOAPAction", "http://www.portalfiscal.inf.br/nfe/wsdl/RecepcaoEvento/recepcaoEvento");

        // Configurar certificado no handler
        var handler = new HttpClientHandler
        {
            ClientCertificates = { certificate },
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds) };

        try
        {
            var response = await client.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return ManifestationResult.Failure($"HTTP {(int)response.StatusCode}: {responseContent}");
            }

            return ParseSoapResponse(responseContent, evento.TipoEvento);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de rede ao enviar evento {TipoEvento} para SEFAZ", evento.TipoEvento);
            return ManifestationResult.Failure($"Erro de rede: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout ao enviar evento {TipoEvento} para SEFAZ", evento.TipoEvento);
            return ManifestationResult.Failure("Timeout na comunicação com SEFAZ");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao enviar evento {TipoEvento}", evento.TipoEvento);
            return ManifestationResult.Failure($"Erro inesperado: {ex.Message}");
        }
    }

    public async Task<EventStatus?> GetEventStatusAsync(ChaveAcesso chave, X509Certificate2 certificate, CancellationToken ct)
    {
        // Consulta status do evento via distribuição DF-e ou consulta específica
        // Implementação simplificada - retorna null se não implementado
        return null;
    }

    private string BuildSoapEnvelope(ManifestationEvent evento, X509Certificate2 certificate)
    {
        var ns = "http://www.portalfiscal.inf.br/nfe";
        var eventoXml = BuildEventoXml(evento, certificate);
        
        var envelope = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(XName.Get("Envelope", "http://www.w3.org/2003/05/soap-envelope"),
                new XAttribute(XNamespace.Xmlns + "soap", "http://www.w3.org/2003/05/soap-envelope"),
                new XElement(XName.Get("Header", "http://www.w3.org/2003/05/soap-envelope")),
                new XElement(XName.Get("Body", "http://www.w3.org/2003/05/soap-envelope"),
                    new XElement(XName.Get("recepcaoEvento", ns),
                        new XElement(XName.Get("nfeDadosMsg", ns),
                            eventoXml
                        )
                    )
                )
            )
        );

        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    private XElement BuildEventoXml(ManifestationEvent evento, X509Certificate2 certificate)
    {
        var ns = "http://www.portalfiscal.inf.br/nfe";
        var eventoElement = new XElement(XName.Get("evento", ns),
            new XAttribute("versao", "1.00"),
            new XElement(XName.Get("infEvento", ns),
                new XAttribute("Id", $"ID{evento.TipoEvento}{evento.ChaveAcesso.Value}{evento.SequenciaEvento:D2}"),
                new XElement(XName.Get("cOrgao", ns), _settings.CodigoOrgao),
                new XElement(XName.Get("tpAmb", ns), _settings.TpAmb),
                new XElement(XName.Get("CNPJ", ns), evento.CnpjDestinatario.Value),
                new XElement(XName.Get("chNFe", ns), evento.ChaveAcesso.Value),
                new XElement(XName.Get("dhEvento", ns), evento.DataHoraEvento.ToString("yyyy-MM-ddTHH:mm:sszzz")),
                new XElement(XName.Get("tpEvento", ns), evento.TipoEvento),
                new XElement(XName.Get("nSeqEvento", ns), evento.SequenciaEvento),
                new XElement(XName.Get("detEvento", ns),
                    new XAttribute("versaoEvento", "1.00"),
                    BuildDetEventoContent(evento)
                )
            )
        );

        // Adicionar assinatura digital (XMLDSIG) - simplificado
        // Em produção, usar SignedXml para assinar o infEvento
        return eventoElement;
    }

    private XElement BuildDetEventoContent(ManifestationEvent evento)
    {
        var ns = "http://www.portalfiscal.inf.br/nfe";
        
        return evento.TipoEvento switch
        {
            "210210" => // Ciência da Operação
                new XElement(XName.Get("descEvento", ns), "Ciência da Operação"),
            
            "210200" => // Confirmação da Operação
                new XElement(XName.Get("descEvento", ns), "Confirmação da Operação"),
            
            "210220" => // Desconhecimento da Operação
                new XElement(XName.Get("descEvento", ns), "Desconhecimento da Operação"),
            
            "210240" => // Operação não Realizada
                new XElement(XName.Get("descEvento", ns), "Operação não Realizada",
                    new XElement(XName.Get("xJust", ns), evento.Justificativa ?? "Não informada")
                ),
            
            _ => new XElement(XName.Get("descEvento", ns), "Evento não identificado")
        };
    }

    private ManifestationResult ParseSoapResponse(string soapResponse, string tipoEvento)
    {
        try
        {
            var doc = XDocument.Parse(soapResponse);
            var ns = "http://www.portalfiscal.inf.br/nfe";
            
            // Procurar por retEvento
            var retEvento = doc.Descendants(XName.Get("retEvento", ns)).FirstOrDefault();
            if (retEvento == null)
            {
                return ManifestationResult.Failure("Resposta SEFAZ sem retEvento");
            }

            var cStat = retEvento.Element(XName.Get("cStat", ns))?.Value ?? "";
            var xMotivo = retEvento.Element(XName.Get("xMotivo", ns))?.Value ?? "";
            var chNFe = retEvento.Element(XName.Get("chNFe", ns))?.Value ?? "";
            var nProt = retEvento.Element(XName.Get("nProt", ns))?.Value ?? "";

            var success = cStat == "135"; // 135 = Evento registrado e vinculado à NF-e
            
            if (success)
            {
                return ManifestationResult.Success(
                    protocolo: nProt,
                    cStat: cStat,
                    xMotivo: xMotivo,
                    chaveAcesso: chNFe
                );
            }
            else
            {
                return ManifestationResult.Failure($"Código: {cStat} - {xMotivo}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parsear resposta SOAP da SEFAZ");
            return ManifestationResult.Failure($"Erro ao processar resposta: {ex.Message}");
        }
    }
}