using NEO_e.Domain.ValueObjects;
using NEO_e.Domain.Exceptions;

namespace NEO_e.Domain.Entities;

public sealed class Empresa
{
    public Cnpj Cnpj { get; }
    public string Nome { get; private set; }
    public string CertificadoArquivo { get; private set; }
    public string? CertificadoThumbprint { get; private set; }
    public DateTimeOffset? CertificadoValidade { get; private set; }
    public string? CertificadoSubject { get; private set; }
    public string? CertificadoIssuer { get; private set; }
    public Nsu UltimoNsu { get; private set; }
    public DateTimeOffset? UltimaSincronizacao { get; private set; }
    public EmpresaStatus Status { get; private set; }
    public string? DetalheStatus { get; private set; }
    public bool Selecionada { get; private set; }
    public bool SenhaInformada { get; private set; }

    private Empresa(Cnpj cnpj, string nome, string certificadoArquivo)
    {
        Cnpj = cnpj;
        Nome = nome;
        CertificadoArquivo = certificadoArquivo;
        UltimoNsu = Nsu.Zero;
        Status = EmpresaStatus.Pendente;
    }

    public static Empresa Create(Cnpj cnpj, string nome, string certificadoArquivo)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome da empresa é obrigatório", nameof(nome));
        if (string.IsNullOrWhiteSpace(certificadoArquivo))
            throw new ArgumentException("Arquivo de certificado é obrigatório", nameof(certificadoArquivo));

        return new Empresa(cnpj, nome, certificadoArquivo);
    }

    public void AtualizarCertificadoInfo(string thumbprint, DateTimeOffset validade, string subject, string issuer)
    {
        CertificadoThumbprint = thumbprint;
        CertificadoValidade = validade;
        CertificadoSubject = subject;
        CertificadoIssuer = issuer;
    }

    public void DefinirSenhaInformada(bool informada)
    {
        SenhaInformada = informada;
    }

    public void Selecionar() => Selecionada = true;
    public void Desselecionar() => Selecionada = false;

    public void AtualizarStatus(EmpresaStatus status, string? detalhe = null)
    {
        Status = status;
        DetalheStatus = detalhe;
    }

    public void AtualizarNsu(Nsu novoNsu)
    {
        if (novoNsu < UltimoNsu)
            throw new SynchronizationException(
                $"NSU não pode regredir: atual={UltimoNsu}, novo={novoNsu}",
                SyncErrorCode.StateCorrupted,
                Cnpj.ToString(),
                novoNsu.Value);

        UltimoNsu = novoNsu;
        UltimaSincronizacao = DateTimeOffset.UtcNow;
    }

    public void ResetarNsu()
    {
        UltimoNsu = Nsu.Zero;
        UltimaSincronizacao = null;
    }

    public bool CertificadoValido => CertificadoValidade.HasValue && CertificadoValidade.Value > DateTimeOffset.UtcNow;

    public bool EstaProntaParaSincronizar =>
        Selecionada &&
        SenhaInformada &&
        CertificadoValido &&
        Status != EmpresaStatus.ErroCertificado &&
        Status != EmpresaStatus.CertificadoExpirado;
}

public enum EmpresaStatus
{
    Pendente,
    CertificadoCarregado,
    SenhaInformada,
    Sincronizando,
    Sucesso,
    ErroCertificado,
    CertificadoExpirado,
    ErroSincronizacao,
    Cancelada,
    Ignorada
}