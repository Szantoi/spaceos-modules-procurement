namespace SpaceOS.Modules.Procurement.Domain.Services;

/// <summary>
/// Service for generating and validating ASN QR payload hashes
/// </summary>
public interface IAsnHashService
{
    /// <summary>
    /// Generates a QR payload with HMACSHA256 hash
    /// </summary>
    /// <param name="asnNumber">ASN number</param>
    /// <param name="poId">Purchase Order ID</param>
    /// <param name="expectedDate">Expected delivery date</param>
    /// <returns>QR payload in format: ASN|PO|DATE|HASH</returns>
    string GenerateQrPayload(string asnNumber, string poId, DateTime expectedDate);

    /// <summary>
    /// Validates a QR payload hash
    /// </summary>
    /// <param name="qrPayload">QR payload to validate</param>
    /// <returns>True if hash is valid, false otherwise</returns>
    bool ValidateQrPayload(string qrPayload);

    /// <summary>
    /// Parses QR payload and extracts components
    /// </summary>
    /// <param name="qrPayload">QR payload</param>
    /// <returns>Tuple of (asnNumber, poId, expectedDate, hash) or null if invalid format</returns>
    (string asnNumber, string poId, DateTime expectedDate, string hash)? ParseQrPayload(string qrPayload);
}
