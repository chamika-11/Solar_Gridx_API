// Smart Solar Microgrid Trading System - password hashing and JWT issuance services.
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.Models;

namespace SolarGridX.Api.Services;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) Create(User user);
}

public sealed class PasswordService : IPasswordService
{
    // Hashes a password with PBKDF2 and a random salt; no password is persisted as plaintext.
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA512, 32);
        return $"v1.210000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    // Verifies a supplied password without leaking comparison timing information.
    public bool Verify(string password, string encodedHash)
    {
        var parts = encodedHash.Split('.');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
            return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA512,
            expected.Length
        );
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public sealed class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    // Issues a short-lived JWT containing identity, role and NIC when present.
    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.Secret) || value.Secret.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Secret must be configured with at least 32 characters."
            );
        var expires = DateTime.UtcNow.AddMinutes(value.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
        };
        claims.Add(new Claim(ClaimTypes.Role, user.Role.ToString()));
        if (!string.IsNullOrWhiteSpace(user.Nic))
            claims.Add(new Claim("nic", user.Nic));
        var token = new JwtSecurityToken(
            value.Issuer,
            value.Audience,
            claims,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(value.Secret)),
                SecurityAlgorithms.HmacSha256
            )
        );
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
