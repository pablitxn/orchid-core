using System.Security.Cryptography;
using Application.Interfaces;

namespace Infrastructure.Auth;

public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string HashPassword(string password)
    {
        using var derive = new Rfc2898DeriveBytes(password, SaltSize, Iterations, HashAlgorithmName.SHA256);
        var salt = Convert.ToBase64String(derive.Salt);
        var key = Convert.ToBase64String(derive.GetBytes(KeySize));
        return string.Join('.', Iterations, salt, key);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var parts = hashedPassword.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iter))
            return false;
        var salt = Convert.FromBase64String(parts[1]);
        var key = Convert.FromBase64String(parts[2]);
        using var derive = new Rfc2898DeriveBytes(providedPassword, salt, iter, HashAlgorithmName.SHA256);
        var keyToCheck = derive.GetBytes(KeySize);
        return CryptographicOperations.FixedTimeEquals(key, keyToCheck);
    }
}