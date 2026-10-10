using System.Globalization;
using System.Security.Cryptography;

namespace SupportDesk.Infrastructure.Services;

/// <summary>PBKDF2-HMAC-SHA256 with a random salt. No reversible passwords stored.</summary>
public sealed class Pbkdf2PasswordService
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Prefix = "pbkdf2-sha256";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations,
            HashAlgorithmName.SHA256, HashSize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string? password, string? encoded)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(encoded)) return false;
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture,
                    out var iterations) || iterations < 100_000 || iterations > 1_000_000)
                return false;
            var salt = Convert.FromBase64String(parts[2]);
            var stored = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltSize || stored.Length != HashSize) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations,
                HashAlgorithmName.SHA256, HashSize);
            return CryptographicOperations.FixedTimeEquals(actual, stored);
        }
        catch (FormatException) { return false; }
    }
}
