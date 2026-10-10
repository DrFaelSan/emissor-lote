using System.ComponentModel;
using System.Text.RegularExpressions;
using NEO_e.Application.Contracts;

namespace NEO_e.WinUI.ViewModels;

public sealed class CertificateRowViewModel : INotifyPropertyChanged
{
    private string _password = string.Empty;
    private bool _selected = true;
    private string? _ultimoNsu;

    public CertificateRowViewModel(CertificateInfo certificate)
    {
        FilePath = certificate.FilePath;
        FileName = certificate.FileName;
        Subject = certificate.Subject ?? "Nao lido";
        Issuer = certificate.Issuer ?? "Nao lido";
        Thumbprint = certificate.Thumbprint ?? "Nao lido";
        ValidUntil = certificate.NotAfter;
        HasPrivateKey = certificate.HasPrivateKey;
        Status = certificate.HasPrivateKey ? "Disponivel" : "Sem chave privada";
        Detail = certificate.ErrorMessage ?? string.Empty;
        IsSimulation = false;
        UltimoNsu = "0";
    }

    private CertificateRowViewModel(string companyName, string cnpj)
    {
        FilePath = $"simulacao://certificado-{cnpj}.pfx";
        FileName = $"certificado-simulado-{cnpj}.pfx";
        Subject = $"{companyName}, CNPJ={cnpj}";
        Issuer = "NEO-e - dados sinteticos";
        Thumbprint = $"TESTE-{cnpj}";
        ValidUntil = null;
        HasPrivateKey = false;
        Status = "TESTE - certificado ficticio";
        Detail = "Sem certificado ou conexao externa.";
        IsSimulation = true;
        UltimoNsu = "0";
    }

    public static CertificateRowViewModel CreateSimulation(string companyName, string cnpj) =>
        new(companyName, cnpj);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FilePath { get; }
    public string FileName { get; }
    public string Subject { get; }
    public string Issuer { get; }
    public string Thumbprint { get; }
    public DateTimeOffset? ValidUntil { get; }
    public string ValidUntilText => ValidUntil?.ToString("d") ?? string.Empty;
    public bool HasPrivateKey { get; }
    public string Status { get; }
    public string Detail { get; }
    public bool IsSimulation { get; }

    public string UltimoNsu
    {
        get => _ultimoNsu ?? "0";
        set
        {
            if (_ultimoNsu == value)
                return;
            _ultimoNsu = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UltimoNsu)));
        }
    }

    public string Cnpj => ExtractCnpj();

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;
            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPassword)));
        }
    }

    public bool HasPassword => !string.IsNullOrEmpty(_password);

    public string ExtractCnpj()
    {
        if (string.IsNullOrWhiteSpace(Subject))
            return string.Empty;

        var cnpjMatch = Regex.Match(Subject, @"CNPJ=(\d{14})", RegexOptions.IgnoreCase);
        if (cnpjMatch.Success)
            return cnpjMatch.Groups[1].Value;

        var serialMatch = Regex.Match(Subject, @"SERIALNUMBER=(\d{14})", RegexOptions.IgnoreCase);
        if (serialMatch.Success)
            return serialMatch.Groups[1].Value;

        return string.Empty;
    }

    public bool IsValidCertificate()
    {
        if (!HasPrivateKey)
            return false;

        if (!ValidUntil.HasValue)
            return false;

        return ValidUntil.Value > DateTimeOffset.UtcNow;
    }
}