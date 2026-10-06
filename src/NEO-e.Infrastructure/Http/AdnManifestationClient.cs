using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NEO_e.Application.Contracts;
using NEO_e.Domain.ValueObjects;
using NEO_e.Domain.Exceptions;
using NEO_e.Infrastructure.Configuration;
using NEO_e.Infrastructure.DependencyInjection;

namespace NEO_e.Infrastructure.Http;

public sealed class AdnManifestationClient : IManifestationClient
{
    private readonly AdnManifestationSettings _settings;
    private readonly NEO_e.Application.Contracts.ILogger _logger;
    private readonly IEnvironmentContext _environment;
    private readonly ConcurrentDictionary<string, HttpClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public AdnManifestationClient(
        IOptions<AdnManifestationSettings> settings, 
        NEO_e.Application.Contracts.ILogger logger, 
        IEnvironmentContext environment)
    {
        _settings = settings.Value;
        _logger = logger;
        _environment = environment;
    }

    public async Task<ManifestationResult> SendEventAsync(ManifestationEvent evento, X509Certificate2 certificate, CancellationToken ct)
    {
        var url = $"contribuinte/NFSe/{evento.ChaveAcesso.Value}/Eventos";
        var client = GetClient(certificate);

        var requestBody = new
        {
            tpEvento = evento.TipoEvento,
            dhEvento = evento.DataHoraEvento.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            nSeqEvento = evento.SequenciaEvento,
            detEvento = new
            {
                versaoEvento = "1.00",
                descEvento = GetEventDescription(evento.TipoEvento),
                xJust = evento.Justificativa
            }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions.Default);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add("Accept", "application/json");

        try
        {
            var response = await client.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("ADN Evento response: {StatusCode}, {Content}", response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                return ParseErrorResponse(response.StatusCode, responseContent);
            }

            return ParseSuccessResponse(responseContent, evento.ChaveAcesso);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de rede ao enviar evento {TipoEvento} para ADN", evento.TipoEvento);
            return ManifestationResult.Failure($"Erro de rede: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout ao enviar evento {TipoEvento} para ADN", evento.TipoEvento);
            return ManifestationResult.Failure("Timeout na comunicação com ADN");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao enviar evento {TipoEvento}", evento.TipoEvento);
            return ManifestationResult.Failure($"Erro inesperado: {ex.Message}");
        }
    }

    public async Task<EventStatus?> GetEventStatusAsync(ChaveAcesso chave, X509Certificate2 certificate, CancellationToken ct)
    {
        var url = $"contribuinte/NFSe/{chave.Value}/Eventos";
        var client = GetClient(certificate);

        try
        {
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), ct);
            
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            var content = await response.Content.ReadAsStringAsync(ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Erro ao consultar eventos ADN: {StatusCode}", response.StatusCode);
                return null;
            }

            var dto = JsonSerializer.Deserialize<AdnEventosResponseDto>(content, JsonOptions.Default);
            if (dto?.Eventos == null || dto.Eventos.Count == 0)
                return null;

            // Retornar o último evento mais relevante
            var ultimoEvento = dto.Eventos.OrderByDescending(e => e.DataHora).First();
            return new EventStatus
            {
                TipoEvento = ultimoEvento.TipoEvento,
                DataHora = ultimoEvento.DataHora,
                XmlEvento = ultimoEvento.XmlEvento
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status de eventos ADN para chave {Chave}", chave.Value);
            return null;
        }
    }

    private HttpClient GetClient(X509Certificate2 certificate)
    {
        var key = certificate.Thumbprint ?? certificate.Subject;
        return _clients.GetOrAdd(key, _ =>
        {
            var handler = new SocketsHttpHandler
            {
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    ClientCertificates = new X509CertificateCollection { certificate }
                }
            };
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(_environment.BaseUrl),
                Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
            };
        });
    }

    private static string GetEventDescription(string tipoEvento) => tipoEvento switch
    {
        "210210" => "Ciência da Operação",
        "210200" => "Confirmação da Operação",
        "210220" => "Desconhecimento da Operação",
        "210240" => "Operação não Realizada",
        _ => "Evento não identificado"
    };

    private ManifestationResult ParseSuccessResponse(string content, ChaveAcesso chave)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<AdnEventoResponseDto>(content, JsonOptions.Default);
            if (dto == null)
                return ManifestationResult.Failure("Resposta ADN inválida (null)");

            return ManifestationResult.Success(
                protocolo: dto.Protocolo ?? dto.IdEvento,
                cStat: dto.CStat?.ToString() ?? "200",
                xMotivo: dto.XMotivo ?? "Sucesso",
                chaveAcesso: chave.Value
            );
        }
        catch (Exception ex)
        {
            return ManifestationResult.Failure($"Erro ao parsear resposta: {ex.Message}");
        }
    }

    private ManifestationResult ParseErrorResponse(HttpStatusCode statusCode, string content)
    {
        try
        {
            var errorDto = JsonSerializer.Deserialize<AdnErrorResponseDto>(content, JsonOptions.Default);
            var message = errorDto?.Error?.Message ?? content;
            return ManifestationResult.Failure($"HTTP {(int)statusCode}: {message}");
        }
        catch
        {
            return ManifestationResult.Failure($"HTTP {(int)statusCode}: {content}");
        }
    }
}

file static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

internal sealed record AdnEventoResponseDto
{
    public string? Protocolo { get; init; }
    public string? IdEvento { get; init; }
    public string? CStat { get; init; }
    public string? XMotivo { get; init; }
}

internal sealed record AdnErrorResponseDto
{
    public AdnErrorDetailDto? Error { get; init; }
}

internal sealed record AdnErrorDetailDto
{
    public string? Code { get; init; }
    public string? Message { get; init; }
    public string? Details { get; init; }
}