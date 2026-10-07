using System.Text.RegularExpressions;

namespace NEO_e.Domain.ValueObjects;

public readonly record struct Cnpj
{
    private static readonly Regex DigitsOnly = new(@"\D", RegexOptions.Compiled);
    private const int FullLength = 14;
    private const int RootLength = 8;

    public string Value { get; }

    private Cnpj(string value)
    {
        Value = value;
    }

    public static Cnpj Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("CNPJ não pode ser vazio", nameof(input));

        var digits = DigitsOnly.Replace(input, "");

        if (digits.Length != FullLength)
            throw new ArgumentException($"CNPJ deve ter {FullLength} dígitos", nameof(input));

        if (!Validate(digits))
            throw new ArgumentException("CNPJ inválido (dígito verificador)", nameof(input));

        return new Cnpj(digits);
    }

    public static bool TryParse(string input, out Cnpj cnpj)
    {
        try
        {
            cnpj = Parse(input);
            return true;
        }
        catch
        {
            cnpj = default;
            return false;
        }
    }

    public string Root => Value[..RootLength];

    public bool IsFilial => Value[RootLength..] != "0001";

    public static bool Validate(string digits)
    {
        if (digits.Length != FullLength) return false;

        var weights1 = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var weights2 = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        var sum1 = 0;
        for (var i = 0; i < 12; i++)
            sum1 += (digits[i] - '0') * weights1[i];

        var digit1 = sum1 % 11 < 2 ? 0 : 11 - (sum1 % 11);
        if (digit1 != digits[12] - '0') return false;

        var sum2 = 0;
        for (var i = 0; i < 13; i++)
            sum2 += (digits[i] - '0') * weights2[i];

        var digit2 = sum2 % 11 < 2 ? 0 : 11 - (sum2 % 11);
        return digit2 == digits[13] - '0';
    }

    public string Format() => $"{Value[..2]}.{Value[2..5]}.{Value[5..8]}/{Value[8..12]}-{Value[12..]}";

    public override string ToString() => Value;

    public static implicit operator string(Cnpj cnpj) => cnpj.Value;
}

public readonly record struct Nsu
{
    public long Value { get; }

    public Nsu(long value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "NSU não pode ser negativo");
        Value = value;
    }

    public static Nsu Zero => new(0);

    public static Nsu From(long value) => new(value);

    public Nsu Next() => new(Value + 1);

    public override string ToString() => Value.ToString();

    public static implicit operator long(Nsu nsu) => nsu.Value;
    public static implicit operator Nsu(long value) => new(value);

    public int CompareTo(Nsu other) => Value.CompareTo(other.Value);
    public bool Equals(Nsu other) => Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator <(Nsu left, Nsu right) => left.Value < right.Value;
    public static bool operator >(Nsu left, Nsu right) => left.Value > right.Value;
    public static bool operator <=(Nsu left, Nsu right) => left.Value <= right.Value;
    public static bool operator >=(Nsu left, Nsu right) => left.Value >= right.Value;
}

public readonly record struct ChaveAcesso
{
    private const int NfeLength = 44;
    private const int NfseLength = 50;
    private static readonly Regex DigitsOnly = new(@"\D", RegexOptions.Compiled);

    public string Value { get; }

    private ChaveAcesso(string value)
    {
        Value = value;
    }

    public static ChaveAcesso Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Chave de acesso não pode ser vazia", nameof(input));

        var digits = DigitsOnly.Replace(input, "");

        if (digits.Length is not NfeLength and not NfseLength)
            throw new ArgumentException($"Chave de acesso deve ter {NfeLength} ou {NfseLength} dígitos", nameof(input));

        return new ChaveAcesso(digits);
    }

    public static bool TryParse(string input, out ChaveAcesso chave)
    {
        try
        {
            chave = Parse(input);
            return true;
        }
        catch
        {
            chave = default;
            return false;
        }
    }

    public Cnpj CnpjEmitente => Cnpj.Parse(Value[6..20]);

    public override string ToString() => Value;

    public static implicit operator string(ChaveAcesso chave) => chave.Value;
}

public readonly record struct ValorMonetario
{
    public decimal Value { get; }

    public ValorMonetario(decimal value)
    {
        Value = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static ValorMonetario Zero => new(0);

    public static ValorMonetario From(decimal value) => new(value);

    public static ValorMonetario operator +(ValorMonetario left, ValorMonetario right) => new(left.Value + right.Value);
    public static ValorMonetario operator -(ValorMonetario left, ValorMonetario right) => new(left.Value - right.Value);
    public static ValorMonetario operator *(ValorMonetario left, decimal right) => new(left.Value * right);
    public static ValorMonetario operator /(ValorMonetario left, decimal right) => new(left.Value / right);

    public bool IsZero => Value == 0;

    public override string ToString() => Value.ToString("F2");

    public static implicit operator decimal(ValorMonetario valor) => valor.Value;
    public static implicit operator ValorMonetario(decimal value) => new(value);
}