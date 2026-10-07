using System.Text;
using NEO_e.Application.Contracts;

namespace NEO_e.Infrastructure.FileSystem;

public sealed class SimulationXmlFileWriter : ISimulationXmlWriter
{
    public SimulationXmlFileWriter(string? outputFolder = null)
    {
        OutputFolder = outputFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NEO-e",
            "Simulacao");
    }

    public string OutputFolder { get; }

    public async Task<string> WriteAsync(
        Guid executionId,
        string fileName,
        string content,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Nome de arquivo de simulacao invalido.", nameof(fileName));
        }

        var executionFolder = Path.Combine(OutputFolder, executionId.ToString("N"));
        Directory.CreateDirectory(executionFolder);
        var filePath = Path.Combine(executionFolder, fileName);
        var temporaryPath = Path.Combine(executionFolder, $"{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(content.AsMemory(), ct);
                await writer.FlushAsync(ct);
            }

            ct.ThrowIfCancellationRequested();
            File.Move(temporaryPath, filePath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return filePath;
    }
}
