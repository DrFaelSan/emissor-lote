using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NEO_e.Application.Contracts;
using NEO_e.Domain.ValueObjects;
using NEO_e.Domain.Exceptions;
using NEO_e.Domain.Entities;
using NEO_e.Infrastructure.Configuration;
using NEO_e.Infrastructure.Logging;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;
using NEO_e.Infrastructure.DependencyInjection;

namespace NEO_e.Infrastructure.Http;

public sealed class AdnHttpClient : IAdnClient
{
    private readonly AdnSettings _settings;
    private readonly ILogger _logger;
    private readonly IEnvironmentContext _environment;
    private readonly ConcurrentDictionary<string, HttpClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public AdnHttpClient(IOptions<AdnSettings> settings, ILogger logger, IEnvironmentContext environment)
    {
        _settings = settings.Value;
        _logger = logger;
        _environment = environment;
    }

    public async Task<DfeDistributionResponse> GetDfeAsync(Cnpj cnpj, Nsu nsu, X509Certificate2 certificate, CancellationToken ct)
    {
        var url = $"contribuinte/DFe/{nsu.Value}";
        if (!string.IsNullOrWhiteSpace(cnpj.Value))
        {
            url += $"?cnpjConsulta={cnpj.Value}";
        }

        var client = GetClient(certificate);
        var response = await ExecuteWithRetryAsync(client, () => CreateRequest(HttpMethod.Get, url), ct);

        var content = await response.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("ADN response received: {StatusCode}, {ContentLength} bytes", response.StatusCode, content.Length);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, content, cnpj.Value, nsu.Value);
        }

        var dto = JsonSerializer.Deserialize<AdnDfeResponseDto>(content, JsonOptions.Default);
        if (dto is null)
            throw new SynchronizationException("Resposta da API inválida (null)", SyncErrorCode.InvalidResponse, cnpj.Value, nsu.Value);

        return MapToResponse(dto);
    }

    public async Task<EventosResponse?> GetEventosAsync(ChaveAcesso chave, X509Certificate2 certificate, CancellationToken ct)
    {
        var url = $"contribuinte/NFSe/{chave.Value}/Eventos";

        var client = GetClient(certificate);
        var response = await ExecuteWithRetryAsync(client, () => CreateRequest(HttpMethod.Get, url), ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, content, null, null);
        }

        var dto = JsonSerializer.Deserialize<AdnEventosResponseDto>(content, JsonOptions.Default);
        return dto is null ? null : MapEventos(dto);
    }

    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(HttpClient client, Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= _settings.Retry.MaxAttempts)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var request = requestFactory();
                var response = await client.SendAsync(request, ct);

                if (_settings.Retry.RetryableStatusCodes.Contains((int)response.StatusCode))
                {
                    if (attempt >= _settings.Retry.MaxAttempts)
                        return response;

                    var delay = CalculateDelay(attempt, response);
                    _logger.LogWarning("ADN retornou {StatusCode}, tentativa {Attempt}/{MaxAttempts}, aguardando {Delay}ms",
                        response.StatusCode, attempt + 1, _settings.Retry.MaxAttempts + 1, delay.TotalMilliseconds);

                    await Task.Delay(delay, ct);
                    attempt++;
                    continue;
                }

                return response;
            }
            catch (HttpRequestException ex) when (attempt < _settings.Retry.MaxAttempts)
            {
                lastException = ex;
                var delay = CalculateDelay(attempt, null);
                _logger.LogWarning("Erro de rede ao chamar ADN: {Message}, tentativa {Attempt}/{MaxAttempts}, aguardando {Delay}ms",
                    ex.Message, attempt + 1, _settings.Retry.MaxAttempts + 1, delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
                attempt++;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < _settings.Retry.MaxAttempts)
            {
                var delay = CalculateDelay(attempt, null);
                _logger.LogWarning("Timeout ao chamar ADN, tentativa {Attempt}/{MaxAttempts}, aguardando {Delay}ms",
                    attempt + 1, _settings.Retry.MaxAttempts + 1, delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
                attempt++;
            }
        }

        throw lastException ?? new SynchronizationException("Máximo de tentativas excedido", SyncErrorCode.NetworkError);
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

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Accept", "application/json");
        return request;
    }

    private TimeSpan CalculateDelay(int attempt, HttpResponseMessage? response)
    {
        if (response is not null && _settings.Retry.RespectRetryAfter &&
            response.Headers.TryGetValues("Retry-After", out var values) &&
            int.TryParse(values.FirstOrDefault(), out var retryAfter))
        {
            return TimeSpan.FromSeconds(retryAfter);
        }

        var delayMs = Math.Min(
            _settings.Retry.BaseDelayMs * Math.Pow(_settings.Retry.BackoffMultiplier, attempt),
            _settings.Retry.MaxDelayMs);

        var jitter = Random.Shared.NextDouble() * 0.3 * delayMs;
        return TimeSpan.FromMilliseconds(delayMs + jitter);
    }

    private static SynchronizationException CreateApiException(HttpStatusCode statusCode, string content, string? cnpj, long? nsu)
    {
        var errorCode = statusCode switch
        {
            HttpStatusCode.BadRequest => SyncErrorCode.InvalidResponse,
            HttpStatusCode.Unauthorized => SyncErrorCode.ApiError,
            HttpStatusCode.Forbidden => SyncErrorCode.ApiError,
            HttpStatusCode.NotFound => SyncErrorCode.InvalidResponse,
            HttpStatusCode.TooManyRequests => SyncErrorCode.RateLimited,
            _ when (int)statusCode >= 500 => SyncErrorCode.ApiError,
            _ => SyncErrorCode.ApiError
        };

        var message = $"API ADN retornou {(int)statusCode} {statusCode}";
        return new SynchronizationException(message, errorCode, cnpj, nsu);
    }

    private static DfeDistributionResponse MapToResponse(AdnDfeResponseDto dto)
    {
        return new DfeDistributionResponse(
            new Nsu(dto.UltNsu),
            dto.MaxNsu,
            dto.Lote?.Select(MapDocument).ToList() ?? []);
    }

    private static DfeDocument MapDocument(AdnDfeDocumentDto dto)
    {
        return new DfeDocument(
            new Nsu(dto.Nsu),
            ChaveAcesso.Parse(dto.ChaveAcesso),
            Enum.TryParse<TipoDocumento>(dto.TipoDocumento, out var tipo) ? tipo : TipoDocumento.NfseRecebida,
            DateTimeOffset.Parse(dto.DataHora),
            dto.Xml);
    }

    private static EventosResponse MapEventos(AdnEventosResponseDto dto)
    {
        return new EventosResponse(
            dto.Eventos?.Select(e => new EventoDocumento(
                e.TipoEvento,
                DateTimeOffset.Parse(e.DataHora),
                e.XmlEvento)).ToList() ?? []);
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

internal sealed record AdnDfeResponseDto
{
    public long UltNsu { get; init; }
    public long MaxNsu { get; init; }
    public IReadOnlyList<AdnDfeDocumentDto>? Lote { get; init; }
}

internal sealed record AdnDfeDocumentDto
{
    public long Nsu { get; init; }
    public string ChaveAcesso { get; init; } = string.Empty;
    public string TipoDocumento { get; init; } = string.Empty;
    public string DataHora { get; init; } = string.Empty;
    public string Xml { get; init; } = string.Empty;
}

internal sealed record AdnEventosResponseDto
{
    public IReadOnlyList<AdnEventoDto>? Eventos { get; init; }
}

internal sealed record AdnEventoDto
{
    public string TipoEvento { get; init; } = string.Empty;
    public string DataHora { get; init; } = string.Empty;
    public string XmlEvento { get; init; } = string.Empty;
}