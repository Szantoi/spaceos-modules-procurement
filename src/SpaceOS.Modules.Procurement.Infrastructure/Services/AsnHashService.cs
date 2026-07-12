using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SpaceOS.Modules.Procurement.Domain.Services;

namespace SpaceOS.Modules.Procurement.Infrastructure.Services;

public class AsnHashService : IAsnHashService
{
    private readonly string _secret;

    public AsnHashService(IConfiguration configuration)
    {
        _secret = configuration["ASN_SECRET"]
            ?? throw new InvalidOperationException("ASN_SECRET environment variable is required.");

        if (_secret.Length < 32)
            throw new InvalidOperationException("ASN_SECRET must be at least 32 characters long.");
    }

    public string GenerateQrPayload(string asnNumber, string poId, DateTime expectedDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(asnNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(poId);

        var dateString = expectedDate.ToString("yyyy-MM-dd");
        var dataToHash = $"{asnNumber}|{poId}|{dateString}";
        var hash = ComputeHmacSha256(dataToHash);

        return $"{dataToHash}|{hash}";
    }

    public bool ValidateQrPayload(string qrPayload)
    {
        var parsed = ParseQrPayload(qrPayload);
        if (parsed == null) return false;

        var (asnNumber, poId, expectedDate, providedHash) = parsed.Value;
        var dateString = expectedDate.ToString("yyyy-MM-dd");
        var dataToHash = $"{asnNumber}|{poId}|{dateString}";
        var expectedHash = ComputeHmacSha256(dataToHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash),
            Encoding.UTF8.GetBytes(providedHash)
        );
    }

    public (string asnNumber, string poId, DateTime expectedDate, string hash)? ParseQrPayload(string qrPayload)
    {
        if (string.IsNullOrWhiteSpace(qrPayload)) return null;

        var parts = qrPayload.Split('|');
        if (parts.Length != 4) return null;

        var asnNumber = parts[0];
        var poId = parts[1];
        var dateString = parts[2];
        var hash = parts[3];

        if (string.IsNullOrWhiteSpace(asnNumber) ||
            string.IsNullOrWhiteSpace(poId) ||
            string.IsNullOrWhiteSpace(hash))
            return null;

        if (!DateTime.TryParse(dateString, out var expectedDate))
            return null;

        return (asnNumber, poId, expectedDate, hash);
    }

    private string ComputeHmacSha256(string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
