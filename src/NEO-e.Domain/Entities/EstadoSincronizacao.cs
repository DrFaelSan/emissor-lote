using NEO_e.Domain.ValueObjects;

namespace NEO_e.Domain.Entities;

public sealed class EstadoSincronizacao
{
    public Cnpj Cnpj { get; }
    public Nsu UltimoNsuConfirmado { get; private set; }
    public Nsu UltimoNsuConsultado { get; private set; }
    public long MaxNsu { get; private set; }
    public DateTimeOffset UltimaSincronizacao { get; private set; }
    public int Versao { get; private set; }
    public DateTimeOffset DataCriacao { get; }

    private EstadoSincronizacao(Cnpj cnpj)
    {
        Cnpj = cnpj;
        UltimoNsuConfirmado = Nsu.Zero;
        UltimoNsuConsultado = Nsu.Zero;
        MaxNsu = 0;
        UltimaSincronizacao = DateTimeOffset.MinValue;
        Versao = 1;
        DataCriacao = DateTimeOffset.UtcNow;
    }

    public static EstadoSincronizacao Create(Cnpj cnpj) => new(cnpj);

    public static EstadoSincronizacao Create(Cnpj cnpj, Nsu ultimoNsu, long maxNsu, DateTimeOffset ultimaSync, int versao = 1)
    {
        var estado = new EstadoSincronizacao(cnpj)
        {
            UltimoNsuConfirmado = ultimoNsu,
            UltimoNsuConsultado = ultimoNsu,
            MaxNsu = maxNsu,
            UltimaSincronizacao = ultimaSync,
            Versao = versao
        };
        return estado;
    }

    public void AtualizarProgresso(Nsu nsuConsultado, long maxNsu)
    {
        if (nsuConsultado > UltimoNsuConsultado)
            UltimoNsuConsultado = nsuConsultado;

        if (maxNsu > MaxNsu)
            MaxNsu = maxNsu;
    }

    public void ConfirmarNsu(Nsu nsuConfirmado)
    {
        if (nsuConfirmado > UltimoNsuConfirmado)
            UltimoNsuConfirmado = nsuConfirmado;

        UltimaSincronizacao = DateTimeOffset.UtcNow;
        Versao++;
    }

    public bool HaProgresso(Nsu nsuConsultado) => nsuConsultado > UltimoNsuConsultado;

    public bool CaixaVazia => UltimoNsuConsultado >= MaxNsu && MaxNsu > 0;

    public bool PrecisaSincronizar => UltimoNsuConfirmado < MaxNsu;
}