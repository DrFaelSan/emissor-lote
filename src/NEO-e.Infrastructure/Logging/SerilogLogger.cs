using NEO_e.Application.Contracts;

namespace NEO_e.Infrastructure.Logging;

public sealed class SerilogLogger : ILogger
{
    private readonly Serilog.ILogger _logger;

    public SerilogLogger(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public void LogDebug(string message, params object?[] args) => _logger.Debug(message, args);
    public void LogInformation(string message, params object?[] args) => _logger.Information(message, args);
    public void LogWarning(string message, params object?[] args) => _logger.Warning(message, args);
    public void LogError(string message, params object?[] args) => _logger.Error(message, args);
    public void LogError(Exception exception, string message, params object?[] args) => _logger.Error(exception, message, args);
}

public static class LoggerExtensions
{
    public static ILogger ForContext<T>(this ILogger logger) => logger;
}