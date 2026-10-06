using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NEO_e.Application.Contracts;
using NEO_e.Domain.ValueObjects;
using NEO_e.Infrastructure.Http;

namespace NEO_e.IntegrationTests.Fakes;

public sealed class FakeAdnHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _routes = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public FakeAdnHandler()
    {
        SetupDefaultRoutes();
    }

    private void SetupDefaultRoutes()
    {
        // Default: return empty lote
        _routes["GET:/contribuinte/DFe/"] = (req, ct) => Task.FromResult(CreateResponse(HttpStatusCode.OK, new AdnDfeResponseDto
        {
            UltNsu = 0,
            MaxNsu = 0,
            Lote = []
        }));
    }

    internal void SetupSuccess(long ultNsu, long maxNsu, params AdnDfeDocumentDto[] documents)
    {
        _routes["GET:/contribuinte/DFe/"] = (req, ct) => Task.FromResult(CreateResponse(HttpStatusCode.OK, new AdnDfeResponseDto
        {
            UltNsu = ultNsu,
            MaxNsu = maxNsu,
            Lote = documents.ToList()
        }));
    }

    public void SetupEmptyLote(long ultNsu = 0, long maxNsu = 0)
    {
        _routes["GET:/contribuinte/DFe/"] = (req, ct) => Task.FromResult(CreateResponse(HttpStatusCode.OK, new AdnDfeResponseDto
        {
            UltNsu = ultNsu,
            MaxNsu = maxNsu,
            Lote = []
        }));
    }

    public void SetupError(HttpStatusCode statusCode, string? errorMessage = null, string? retryAfter = null)
    {
        _routes["GET:/contribuinte/DFe/"] = (req, ct) =>
        {
            var response = CreateResponse(statusCode, new AdnErrorDto
            {
                Error = new AdnErrorDetail
                {
                    Code = ((int)statusCode).ToString(),
                    Message = errorMessage ?? GetDefaultMessage(statusCode),
                    Details = retryAfter != null ? $"Retry-After: {retryAfter}" : null
                }
            });
            
            if (retryAfter != null)
                response.Headers.Add("Retry-After", retryAfter);
                
            return Task.FromResult(response);
        };
    }

    public void SetupTimeout()
    {
        _routes["GET:/contribuinte/DFe/"] = async (req, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.RequestTimeout);
        };
    }

    public void SetupInvalidPayload()
    {
        _routes["GET:/contribuinte/DFe/"] = (req, ct) => Task.FromResult(CreateResponse(HttpStatusCode.OK, "não é json válido {"));
    }

    internal void SetupEventosSuccess(string chaveAcesso, params AdnEventoDto[] eventos)
    {
        _routes[$"GET:/contribuinte/NFSe/{chaveAcesso}/Eventos"] = (req, ct) => Task.FromResult(CreateResponse(HttpStatusCode.OK, new AdnEventosResponseDto
        {
            Eventos = eventos.ToList()
        }));
    }

    public void SetupEventosNotFound()
    {
        _routes["GET:/contribuinte/NFSe/"] = (req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        var method = request.Method.Method;
        var key = $"{method}:{path}";

        // Find matching route (supporting path prefix matching)
        var matchedRoute = _routes.Keys.FirstOrDefault(k => path.StartsWith(k.Replace("GET:", "").Replace("POST:", ""), StringComparison.OrdinalIgnoreCase));
        if (matchedRoute != null)
        {
            var handler = _routes[matchedRoute];
            return await handler(request, cancellationToken);
        }

        // Default fallback
        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Route not found in fake handler", Encoding.UTF8, "text/plain")
        };
    }

    private HttpResponseMessage CreateResponse<T>(HttpStatusCode statusCode, T content)
    {
        var json = JsonSerializer.Serialize(content, _jsonOptions);
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return response;
    }

    private static string GetDefaultMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "Requisição inválida",
        HttpStatusCode.Unauthorized => "Certificado digital inválido ou não apresentado",
        HttpStatusCode.Forbidden => "Acesso negado: certificado não autorizado para o CNPJ consultado",
        HttpStatusCode.NotFound => "Não encontrado",
        HttpStatusCode.TooManyRequests => "Rate limit excedido",
        HttpStatusCode.InternalServerError => "Erro interno do servidor",
        _ => "Erro desconhecido"
    };
}

internal sealed record AdnErrorDto
{
    public AdnErrorDetail Error { get; init; } = new();
}

internal sealed record AdnErrorDetail
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
}

// DTOs reutilizados do AdnHttpClient
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

// Helper para criar XMLs compactados (GZip + Base64) para testes
public static class FakeXmlHelper
{
    public static string CreateCompressedNfseXml(string chaveAcesso, string cnpjEmitente, string cnpjDestinatario, string numero, string serie, DateTimeOffset dataEmissao, decimal valorTotal)
    {
        var xml = $"""
            <NFSe xmlns="urn:neo-e:test">
              <chNFSe>{chaveAcesso}</chNFSe>
              <CNPJPrestador>{cnpjEmitente}</CNPJPrestador>
              <CNPJTomador>{cnpjDestinatario}</CNPJTomador>
              <nNFSe>{numero}</nNFSe>
              <serie>{serie}</serie>
              <dhEmi>{dataEmissao:yyyy-MM-ddTHH:mm:sszzz}</dhEmi>
              <vLiq>{valorTotal:F2}</vLiq>
            </NFSe>
            """;

        using var output = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(xml);
            gzip.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(output.ToArray());
    }
}